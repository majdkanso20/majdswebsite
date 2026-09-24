using MajdsApp.SharedKernel.Notifications;
using System.Collections.Concurrent;
using System.Diagnostics;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Modules;

namespace MajdsApp.Modules.Jobs;

/// <summary>Runs one job and records the outcome. A per-name guard stops a scheduled tick and a manual
/// "run now" (or two ticks) from running the same job concurrently. The guard is in-process, so this
/// assumes a single API instance — multi-instance deployments would need a distributed lock.</summary>
public class JobExecutor(ApplicationDbContext db, IUserNotificationPublisher notifications) : IScopedService
{
    private static readonly ConcurrentDictionary<string, bool> Running = new();

    /// <returns>false if the job was already running and this call was skipped.</returns>
    public async Task<bool> RunAsync(IRecurringJob job, string trigger, CancellationToken ct)
    {
        if (!Running.TryAdd(job.Name, true))
            return false;

        var run = new JobRun { JobName = job.Name, StartedAt = DateTime.UtcNow, Trigger = trigger };
        var clock = Stopwatch.StartNew();
        try
        {
            await job.ExecuteAsync(ct);
            run.Success = true;
        }
        catch (Exception ex)
        {
            run.Success = false;
            run.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
        }
        finally
        {
            Running.TryRemove(job.Name, out _);
        }

        run.DurationMs = (int)clock.ElapsedMilliseconds;
        db.Set<JobRun>().Add(run);
        await db.SaveChangesAsync(CancellationToken.None);

        if (!run.Success)
            await notifications.PublishToRoleAsync("Admin", "Background job failed", $"{job.Name}: {run.Error}", NotificationTypes.Administration, CancellationToken.None);

        return true;
    }
}
