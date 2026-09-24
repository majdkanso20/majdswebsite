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

        try
        {
            var response = await next(ct);
            logger.LogInformation("Handled {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{RequestName} failed", requestName);
            throw;
        }
    }
}
