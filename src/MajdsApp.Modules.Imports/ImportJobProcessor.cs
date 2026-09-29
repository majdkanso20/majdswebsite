using System.Text.Json;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Import;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Imports;

/// <summary>
/// Runs one queued import at a time (FR-EXP-003/004): parses the job's file through its source and records the
/// per-row outcome, then notifies the user with a link to their import history. Separate from the hosted worker
/// so it can be driven directly, and a job that throws (a whole-file problem, not a row problem) is recorded as
/// failed rather than lost.
/// </summary>
public class ImportJobProcessor(
    ApplicationDbContext db, IEnumerable<IImportSource> sources, IUserNotificationPublisher notifications, ILogger<ImportJobProcessor> logger) : IScopedService
{
    /// <summary>Claims and runs the oldest pending job. Returns false when there was nothing to do.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var next = await db.Set<ImportJob>().Where(j => j.Status == ImportJobStatus.Pending)
            .OrderBy(j => j.CreatedAt).Select(j => j.Id).FirstOrDefaultAsync(ct);
        if (next == Guid.Empty) return false;

        // Claim atomically so two instances never run the same job.
        var claimed = await db.Set<ImportJob>().Where(j => j.Id == next && j.Status == ImportJobStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ImportJobStatus.Running).SetProperty(j => j.StartedAt, DateTime.UtcNow), ct);
        if (claimed == 0) return true;

        var job = await db.Set<ImportJob>().FirstAsync(j => j.Id == next, ct);
        try
        {
            var source = sources.FirstOrDefault(s => s.Key == job.Source)
                ?? throw new InvalidOperationException($"The '{job.Source}' import is no longer available.");

            var result = await source.RunAsync(job.Content, job.FileName, ct);

            job.Total = result.Total;
            job.Succeeded = result.Succeeded;
            job.Errors = result.Errors.Count > 0 ? JsonSerializer.Serialize(result.Errors) : null;
            job.Content = []; // the file has done its job; no reason to keep it once it has run
            job.Status = ImportJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = null;
            await db.SaveChangesAsync(ct);

            var summary = result.Failed == 0
                ? $"{result.Succeeded:N0} of {result.Total:N0} rows imported."
                : $"{result.Succeeded:N0} of {result.Total:N0} rows imported; {result.Failed:N0} failed.";
            await notifications.PublishAsync(job.UserId, $"Your {job.Title} import finished", summary, NotificationTypes.General, ct, link: "/imports");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Import job {JobId} ({Source}) failed", job.Id, job.Source);
            job.Status = ImportJobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
            job.Content = [];
            await db.SaveChangesAsync(CancellationToken.None);

            await notifications.PublishAsync(job.UserId, $"Your {job.Title} import failed",
                $"Open Import history for details.", NotificationTypes.General, CancellationToken.None, link: "/imports");
        }

        return true;
    }

    /// <summary>After a crash or restart, a job left Running would never finish; put it back in the queue.</summary>
    public Task<int> RequeueInterruptedAsync(CancellationToken ct) =>
        db.Set<ImportJob>().Where(j => j.Status == ImportJobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ImportJobStatus.Pending), ct);
}
