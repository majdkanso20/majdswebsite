using MajdsApp.SharedKernel.Files;
using System.Text.Json;
using MajdsApp.Data;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Export;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Exports;

public static class ExportLimits
{
    /// <summary>Background exports go well beyond the immediate download's cap; the file is streamed to disk, not held for the client.</summary>
    public const int BackgroundMaxRows = 200_000;
}

/// <summary>
/// Runs one queued export at a time (FR-EXP-004): builds the file from the job's source and filters, stores it in F-Files
/// as the requesting user's file, and notifies them with a link to the exports list. Separate from the hosted worker so it
/// can be driven directly, and a job that throws is recorded as failed (and the user told) rather than lost.
/// </summary>
public class ExportJobProcessor(
    ApplicationDbContext db, IEnumerable<IExportSource> sources, IFileStorage storage,
    IUserNotificationPublisher notifications, ILogger<ExportJobProcessor> logger) : IScopedService
{
    /// <summary>Claims and runs the oldest pending job. Returns false when there was nothing to do.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var next = await db.Set<ExportJob>().Where(j => j.Status == ExportJobStatus.Pending)
            .OrderBy(j => j.CreatedAt).Select(j => j.Id).FirstOrDefaultAsync(ct);
        if (next == Guid.Empty) return false;

        // Claim atomically so two instances never run the same job.
        var claimed = await db.Set<ExportJob>().Where(j => j.Id == next && j.Status == ExportJobStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ExportJobStatus.Running).SetProperty(j => j.StartedAt, DateTime.UtcNow), ct);
        if (claimed == 0) return true;

        var job = await db.Set<ExportJob>().FirstAsync(j => j.Id == next, ct);
        try
        {
            var source = sources.FirstOrDefault(s => s.Key == job.Source)
                ?? throw new InvalidOperationException($"The '{job.Source}' export is no longer available.");
            var filters = JsonSerializer.Deserialize<Dictionary<string, string>>(job.Filters) ?? [];

            var file = await source.BuildAsync(ExportFormats.Parse(job.Format), filters, ExportLimits.BackgroundMaxRows, ct);

            var record = new FileRecord
            {
                Id = Guid.NewGuid(),
                FileName = Path.GetFileNameWithoutExtension(file.FileName) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmm") + Path.GetExtension(file.FileName),
                ContentType = file.ContentType,
                Size = file.Content.Length,
                StoredName = Guid.NewGuid().ToString("N"),
                OwnerId = job.UserId,
                OwnerName = job.UserName,
                CreatedAt = DateTime.UtcNow
            };
            await using (var content = new MemoryStream(file.Content))
                await storage.SaveAsync(record.StoredName, content, ct);

            db.Set<FileRecord>().Add(record);
            job.FileId = record.Id;
            job.Status = ExportJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = null;
            await db.SaveChangesAsync(ct);

            await notifications.PublishAsync(job.UserId, "Your export is ready",
                $"The {job.Title} export ({record.FileName}) is ready to download.", NotificationTypes.General, ct, link: "/exports");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Export job {JobId} ({Source}) failed", job.Id, job.Source);
            job.Status = ExportJobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);

            await notifications.PublishAsync(job.UserId, "Your export failed",
                $"The {job.Title} export could not be created. Open Exports for details.", NotificationTypes.General, CancellationToken.None, link: "/exports");
        }

        return true;
    }

    /// <summary>After a crash or restart, a job left Running would never finish; put it back in the queue.</summary>
    public Task<int> RequeueInterruptedAsync(CancellationToken ct) =>
        db.Set<ExportJob>().Where(j => j.Status == ExportJobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ExportJobStatus.Pending), ct);
}
