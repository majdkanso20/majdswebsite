using System.Diagnostics;
using MajdsApp.SharedKernel.Middleware;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>Logs entry/exit and failures for every request (FR-XC-002) so handlers never log this themselves.</summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        // One span per request, nested under the HTTP request's own, so a trace shows which command or query took the time.
        using var span = PlatformTelemetry.Source.StartActivity(requestName);
        try
        {
            var response = await next(ct);
            logger.LogInformation("Handled {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            span?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            logger.LogWarning(ex, "{RequestName} failed", requestName);
            throw;
        }
    }
}
