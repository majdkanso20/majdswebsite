using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Jobs;

/// <summary>Drains the persisted job queue in the background. Queued jobs are work someone asked for, so they run regardless of the
/// <c>Jobs.Enabled</c> switch, which only governs the recurring schedule.</summary>
public class BackgroundJobWorker(IServiceScopeFactory scopes, ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().RequeueInterruptedAsync(stoppingToken);
            if (requeued > 0) logger.LogWarning("Re-queued {Count} background job(s) interrupted by a restart", requeued);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not re-queue interrupted background jobs");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;
            try
            {
                using var scope = scopes.CreateScope();
                didWork = await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ProcessNextAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Background job worker iteration failed");
            }

            if (didWork) continue;

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
