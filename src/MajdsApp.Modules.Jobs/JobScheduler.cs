using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Jobs;

/// <summary>Wakes every 30s and runs any job that is due (see <see cref="JobSchedule"/>): its interval or cron time has come, it has
/// never run, or its last run failed and a retry is due. "Last run" comes from the database, so restarts don't reset the schedule.</summary>
public class JobScheduler(IServiceScopeFactory scopes, ILogger<JobScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the host time to finish starting (migrations, seeding) before the first tick.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ContinueWith(_ => { });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job scheduler tick failed");
            }

            await Task.Delay(Tick, stoppingToken).ContinueWith(_ => { });
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();
        if (!await settings.GetBooleanAsync("Jobs.Enabled", ct))
            return;

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<JobExecutor>();

        foreach (var job in scope.ServiceProvider.GetServices<IRecurringJob>())
        {
            var recent = await db.Set<JobRun>().AsNoTracking().Where(r => r.JobName == job.Name)
                .OrderByDescending(r => r.StartedAt).Take(JobSchedule.MaxRetries + 2).ToListAsync(ct);

            var (due, trigger) = JobSchedule.Evaluate(job, recent, DateTime.UtcNow);
            if (due)
                await executor.RunAsync(job, trigger, ct);
        }
    }
}
