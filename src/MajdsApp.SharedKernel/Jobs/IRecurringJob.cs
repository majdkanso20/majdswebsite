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

    /// <summary>How often it runs, unless <see cref="Cron"/> is set. A failed run is retried sooner, with growing delays,
    /// before the next scheduled run (FR-JOB-002).</summary>
    TimeSpan Interval { get; }

    /// <summary>Optional cron expression (five fields, UTC, for example <c>0 3 * * *</c> for 03:00 every day). When set it
    /// decides when the job runs and <see cref="Interval"/> is only shown as a hint.</summary>
    string? Cron => null;

    Task ExecuteAsync(CancellationToken ct);
}
