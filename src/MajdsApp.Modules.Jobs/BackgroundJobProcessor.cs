using System.Text.Json;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Jobs;

/// <summary>Queues one-off jobs (FR-JOB-001). The job is written to the database, so it is not lost if the process stops, and
/// it carries the caller's identity (FR-JOB-004).</summary>
public class BackgroundJobQueue(ApplicationDbContext db, IEnumerable<IBackgroundJob> handlers, ICurrentUser currentUser) : IBackgroundJobQueue
{
    public const int MaxParametersLength = 4000;

    public async Task<Guid> EnqueueAsync(string type, IReadOnlyDictionary<string, string>? parameters = null, TimeSpan? delay = null, CancellationToken ct = default)
    {
        var handler = handlers.FirstOrDefault(h => h.Type == type)
            ?? throw new ArgumentException($"There is no background job of type '{type}'.", nameof(type));

        var json = JsonSerializer.Serialize(parameters ?? new Dictionary<string, string>());
        if (json.Length > MaxParametersLength)
            throw new ArgumentException($"The job parameters are larger than {MaxParametersLength} characters.", nameof(parameters));

        var now = DateTime.UtcNow;
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            Type = type,
            Parameters = json,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Status = BackgroundJobStatus.Pending,
            MaxAttempts = Math.Max(1, handler.MaxAttempts),
            CreatedAt = now,
            NextAttemptAt = now + (delay is { } d && d > TimeSpan.Zero ? d : TimeSpan.Zero)
        };

        db.Set<BackgroundJob>().Add(job);
        await db.SaveChangesAsync(ct);
        return job.Id;
    }
}

/// <summary>
/// Runs the next due queued job (FR-JOB-002). A failure puts the job back with a growing delay until its attempts are used up, then
/// leaves it failed and tells the administrators (AC-JOB-2). Separate from the hosted worker so it can be driven directly.
/// </summary>
public class BackgroundJobProcessor(
    ApplicationDbContext db, IEnumerable<IBackgroundJob> handlers, IUserNotificationPublisher notifications, ILogger<BackgroundJobProcessor> logger) : IScopedService
{
    /// <summary>Claims and runs the oldest due job. Returns false when nothing was due.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var next = await db.Set<BackgroundJob>().Where(j => j.Status == BackgroundJobStatus.Pending && j.NextAttemptAt <= now)
            .OrderBy(j => j.NextAttemptAt).Select(j => j.Id).FirstOrDefaultAsync(ct);
        if (next == Guid.Empty) return false;

        // Claim atomically so two workers never run the same job.
        var claimed = await db.Set<BackgroundJob>().Where(j => j.Id == next && j.Status == BackgroundJobStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BackgroundJobStatus.Running)
                .SetProperty(j => j.StartedAt, DateTime.UtcNow).SetProperty(j => j.Attempts, j => j.Attempts + 1), ct);
        if (claimed == 0) return true;

        var job = await db.Set<BackgroundJob>().FirstAsync(j => j.Id == next, ct);
        try
        {
            var handler = handlers.FirstOrDefault(h => h.Type == job.Type)
                ?? throw new InvalidOperationException($"There is no background job of type '{job.Type}' any more.");
            var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(job.Parameters) ?? [];

            await handler.ExecuteAsync(new BackgroundJobContext(job.Id, job.UserId, job.UserName, parameters, job.Attempts), ct);

            job.Status = BackgroundJobStatus.Succeeded;
            job.CompletedAt = DateTime.UtcNow;
            job.LastError = null;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Background job {JobId} ({Type}) failed on attempt {Attempt}/{Max}", job.Id, job.Type, job.Attempts, job.MaxAttempts);
            job.LastError = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;

            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = BackgroundJobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                job.Status = BackgroundJobStatus.Pending;
                job.NextAttemptAt = DateTime.UtcNow + RetryBackoff.Delay(job.Attempts);
            }

            await db.SaveChangesAsync(CancellationToken.None);

            if (job.Status == BackgroundJobStatus.Failed)
                await notifications.PublishToRoleAsync("Admin", "Background job failed",
                    $"The job '{job.Type}' failed after {job.Attempts} attempts: {job.LastError}", NotificationTypes.Administration, CancellationToken.None);
        }

        return true;
    }

    /// <summary>After a crash or restart a job left Running would never finish; put it back in the queue (AC-JOB-1). The interrupted try
    /// counts as an attempt, so a job that keeps crashing the process still ends up failed instead of looping forever.</summary>
    public async Task<int> RequeueInterruptedAsync(CancellationToken ct)
    {
        var interrupted = await db.Set<BackgroundJob>().Where(j => j.Status == BackgroundJobStatus.Running).ToListAsync(ct);
        foreach (var job in interrupted)
        {
            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = BackgroundJobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.LastError = "Interrupted by a restart on its last attempt.";
            }
            else
            {
                job.Status = BackgroundJobStatus.Pending;
                job.NextAttemptAt = DateTime.UtcNow;
            }
        }

        if (interrupted.Count > 0) await db.SaveChangesAsync(ct);
        return interrupted.Count;
    }
}
