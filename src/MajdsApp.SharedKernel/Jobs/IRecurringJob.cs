using MajdsApp.SharedKernel.Modules;

namespace MajdsApp.SharedKernel.Jobs;

/// <summary>
/// A unit of recurring background work (F-Background-Jobs). Any module implements this and it's
/// discovered and registered by convention (it's an <see cref="IScopedService"/>); the F-Background-Jobs
/// module's scheduler runs it every <see cref="Interval"/> and records each run. A new job is one class —
/// no scheduler changes.
/// </summary>
public interface IRecurringJob : IScopedService
{
    /// <summary>Stable unique name; it's the job's identity in run history and "run now".</summary>
    string Name { get; }

    TimeSpan Interval { get; }

    Task ExecuteAsync(CancellationToken ct);
}
