using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace MajdsApp.SharedKernel.Resilience;

/// <summary>The <c>Resilience</c> section: how outbound calls (mail, and later any other external service) retry, time out and stop calling a service that is down.</summary>
public class OutboundResilienceOptions
{
    /// <summary>Retries after the first attempt, only for failures that are worth retrying (a dropped connection, a busy server), never for a wrong password.</summary>
    [Range(0, 10, ErrorMessage = "Resilience:MaxRetries must be between 0 and 10.")]
    public int MaxRetries { get; set; } = 2;

    [Range(1, 60_000, ErrorMessage = "Resilience:RetryDelayMilliseconds must be between 1 and 60000.")]
    public int RetryDelayMilliseconds { get; set; } = 500;

    /// <summary>How long one attempt may take.</summary>
    [Range(1, 600, ErrorMessage = "Resilience:TimeoutSeconds must be between 1 and 600.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>After this many failures within <see cref="BreakSamplingSeconds"/> that are at least half of the calls, the circuit opens.</summary>
    [Range(2, 1000, ErrorMessage = "Resilience:BreakMinimumCalls must be between 2 and 1000.")]
    public int BreakMinimumCalls { get; set; } = 5;

    [Range(1, 3600, ErrorMessage = "Resilience:BreakSamplingSeconds must be between 1 and 3600.")]
    public int BreakSamplingSeconds { get; set; } = 30;

    /// <summary>How long the circuit stays open (calls fail at once, without touching the service) before one trial call is let through.</summary>
    [Range(1, 3600, ErrorMessage = "Resilience:BreakSeconds must be between 1 and 3600.")]
    public int BreakSeconds { get; set; } = 30;
}

/// <summary>
/// The one place resilience for outbound calls is defined (P4 FR-XC-006), applied by decorating the service that makes the call rather than by
/// try/catch in each caller: retry with exponential backoff and jitter, a timeout per attempt, and a circuit breaker.
/// </summary>
public static class OutboundResilience
{
    public static ResiliencePipeline Create(string name, OutboundResilienceOptions options, Func<Exception, bool> isTransient, ILogger? logger = null)
    {
        var handle = new PredicateBuilder().Handle<Exception>(isTransient);
        var builder = new ResiliencePipelineBuilder();

        if (options.MaxRetries > 0)
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = handle,
                MaxRetryAttempts = options.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger?.LogWarning("{Service}: attempt {Attempt} failed ({Error}); retrying in {Delay:F1}s",
                        name, args.AttemptNumber + 1, args.Outcome.Exception?.Message, args.RetryDelay.TotalSeconds);
                    return default;
                }
            });

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ShouldHandle = handle,
            FailureRatio = 0.5,
            MinimumThroughput = options.BreakMinimumCalls,
            SamplingDuration = TimeSpan.FromSeconds(options.BreakSamplingSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.BreakSeconds),
            OnOpened = _ =>
            {
                logger?.LogError("{Service}: too many failures, calls are paused for {Seconds}s", name, options.BreakSeconds);
                return default;
            },
            OnClosed = _ =>
            {
                logger?.LogInformation("{Service}: recovered, calls resume", name);
                return default;
            }
        });

        builder.AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));

        return builder.Build();
    }

    /// <summary>True for the exceptions every outbound call treats as "try again": a timeout, a network or IO failure.</summary>
    public static bool IsCommonTransient(Exception ex) =>
        ex is TimeoutRejectedException or TimeoutException or System.IO.IOException or System.Net.Sockets.SocketException or HttpRequestException;
}
