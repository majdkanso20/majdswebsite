using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Imports;

/// <summary>Drains the import queue in the background so a large import never holds a request open (F-Export FR-EXP-004).</summary>
public class ImportWorker(IServiceScopeFactory scopes, ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<ImportJobProcessor>().RequeueInterruptedAsync(stoppingToken);
            if (requeued > 0) logger.LogWarning("Re-queued {Count} import job(s) interrupted by a restart", requeued);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not re-queue interrupted import jobs");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;
            try
            {
                using var scope = scopes.CreateScope();
                didWork = await scope.ServiceProvider.GetRequiredService<ImportJobProcessor>().ProcessNextAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Import worker iteration failed");
            }

            if (didWork) continue; // drain the queue before sleeping

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
