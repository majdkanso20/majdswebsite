using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Exports;

/// <summary>Drains the export queue in the background so a large export never holds a request open (AC-EXP-3).</summary>
public class ExportWorker(IServiceScopeFactory scopes, ILogger<ExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<ExportJobProcessor>().RequeueInterruptedAsync(stoppingToken);
            if (requeued > 0) logger.LogWarning("Re-queued {Count} export job(s) interrupted by a restart", requeued);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not re-queue interrupted export jobs");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;
            try
            {
                using var scope = scopes.CreateScope();
                didWork = await scope.ServiceProvider.GetRequiredService<ExportJobProcessor>().ProcessNextAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Export worker iteration failed");
            }

            if (didWork) continue; // drain the queue before sleeping

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
