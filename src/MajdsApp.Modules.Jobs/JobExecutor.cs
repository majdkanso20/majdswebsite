using MajdsApp.SharedKernel.Notifications;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
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
        {
            // A scheduled job that fails is retried before anyone is bothered; the administrators are told once the retries are used up,
            // or straight away when someone ran it by hand and is waiting for the result.
            var previous = await db.Set<JobRun>().AsNoTracking().Where(r => r.JobName == job.Name && r.Id != run.Id)
                .OrderByDescending(r => r.StartedAt).Take(JobSchedule.MaxRetries + 1).ToListAsync(CancellationToken.None);
            var failuresInARow = JobSchedule.ConsecutiveFailures(previous) + 1;

            if (trigger == "Manual" || failuresInARow > JobSchedule.MaxRetries)
                await notifications.PublishToRoleAsync("Admin", "Background job failed", $"The job '{job.Name}' failed: {run.Error}", NotificationTypes.Administration, CancellationToken.None);
        }

        return true;
    }
}
