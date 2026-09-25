using MajdsApp.SharedKernel.Modules;

namespace MajdsApp.SharedKernel.Jobs;

/// <summary>What a queued job gets when it runs: its own id, the user who queued it (FR-JOB-004), its parameters, and which
/// attempt this is (1 for the first).</summary>
public record BackgroundJobContext(Guid JobId, string? UserId, string? UserName, IReadOnlyDictionary<string, string> Parameters, int Attempt);

/// <summary>
/// A unit of one-off background work (F-Background-Jobs FR-JOB-001): fire-and-forget or delayed. Any module implements one
/// (it is an <see cref="IScopedService"/>, so it is discovered by convention) and queues it by <see cref="Type"/> through
/// <see cref="IBackgroundJobQueue"/>. The queue is persisted, so a queued job survives a restart, and a failure is retried
/// with growing delays until <see cref="MaxAttempts"/> is used up (FR-JOB-002).
/// </summary>
public interface IBackgroundJob : IScopedService
{
    /// <summary>Stable unique name jobs are queued under.</summary>
    string Type { get; }

    /// <summary>Total tries, including the first, before the job is left as failed.</summary>
    int MaxAttempts => 3;

    /// <summary>Do the work. Throw to fail (and be retried). Make it safe to run again: a job interrupted by a restart runs again.</summary>
    Task ExecuteAsync(BackgroundJobContext context, CancellationToken ct);
}

/// <summary>Queues one-off jobs. The caller's identity is recorded with the job and handed back to it when it runs.</summary>
public interface IBackgroundJobQueue
{
    /// <param name="type">The <see cref="IBackgroundJob.Type"/> to run.</param>
    /// <param name="parameters">Small string values the job needs (ids, options). Not for large data.</param>
    /// <param name="delay">Run no earlier than this long from now; null runs as soon as a worker is free.</param>
    Task<Guid> EnqueueAsync(string type, IReadOnlyDictionary<string, string>? parameters = null, TimeSpan? delay = null, CancellationToken ct = default);
}

/// <summary>Used only when the Background Jobs module is not part of the host.</summary>
public class NullBackgroundJobQueue : IBackgroundJobQueue
{
    public Task<Guid> EnqueueAsync(string type, IReadOnlyDictionary<string, string>? parameters = null, TimeSpan? delay = null, CancellationToken ct = default) =>
        throw new InvalidOperationException("The Background Jobs module is not installed, so jobs cannot be queued.");
}
