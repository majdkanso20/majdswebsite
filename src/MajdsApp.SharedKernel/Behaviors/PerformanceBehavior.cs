using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>Warns when a request exceeds the slow-request threshold (FR-XC-002, NFR-PERF-1).</summary>
public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 300;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next(ct);
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            logger.LogWarning("{RequestName} took {ElapsedMs}ms (threshold {ThresholdMs}ms)",
                typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, SlowRequestThresholdMs);
        }

        return response;
    }
}
