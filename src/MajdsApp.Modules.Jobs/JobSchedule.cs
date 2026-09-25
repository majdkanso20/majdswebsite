using Cronos;
using MajdsApp.SharedKernel.Jobs;

namespace MajdsApp.Modules.Jobs;

/// <summary>
/// When a recurring job is due, and how a failing one is retried (F-Background-Jobs FR-JOB-001/002). Pure functions of the job,
/// its recent runs and the clock, so the rules are testable without a database or a running scheduler.
/// </summary>
public static class JobSchedule
{
    /// <summary>Retries after a failed scheduled run, before waiting for the next scheduled time (AC-JOB-2).</summary>
    public const int MaxRetries = 3;

    /// <summary>Delay before retry <paramref name="failures"/>: 1, 2, 4 minutes.</summary>
    public static TimeSpan RetryDelay(int failures) => TimeSpan.FromMinutes(Math.Pow(2, Math.Max(0, failures - 1)));

    /// <summary>How many runs in a row have failed, counting back from the newest.</summary>
    public static int ConsecutiveFailures(IReadOnlyList<JobRun> newestFirst)
    {
        var count = 0;
        foreach (var run in newestFirst)
        {
            if (run.Success) break;
            count++;
        }

        return count;
    }

    /// <summary>The next regular (non-retry) time after <paramref name="lastStart"/>, or null when the job has never run.</summary>
    public static DateTime? NextRegularRun(IRecurringJob job, DateTime? lastStart)
    {
        if (lastStart is null) return null;

        if (!string.IsNullOrWhiteSpace(job.Cron) && TryParse(job.Cron, out var cron))
            return cron.GetNextOccurrence(DateTime.SpecifyKind(lastStart.Value, DateTimeKind.Utc), TimeZoneInfo.Utc);

        return lastStart + job.Interval;
    }

    /// <summary>When the job will next start: the next regular time, or the next retry if that comes first. Null if it has never run.</summary>
    public static DateTime? NextRun(IRecurringJob job, IReadOnlyList<JobRun> newestFirst)
    {
        if (newestFirst.Count == 0) return null;

        var last = newestFirst[0];
        var next = NextRegularRun(job, last.StartedAt);
        if (!last.Success)
        {
            var failures = ConsecutiveFailures(newestFirst);
            if (failures <= MaxRetries)
            {
                var retry = last.StartedAt + RetryDelay(failures);
                if (next is null || retry < next) next = retry;
            }
        }

        return next;
    }

    /// <summary>Decides whether the job should start now and why: <c>Schedule</c> for a regular run, <c>Retry</c> after a failure.
    /// A job that has never run is due at once.</summary>
    public static (bool Due, string Trigger) Evaluate(IRecurringJob job, IReadOnlyList<JobRun> newestFirst, DateTime nowUtc)
    {
        if (newestFirst.Count == 0) return (true, "Schedule");

        var last = newestFirst[0];
        var next = NextRegularRun(job, last.StartedAt);
        if (next is { } regular && nowUtc >= regular) return (true, "Schedule");

        if (!last.Success)
        {
            var failures = ConsecutiveFailures(newestFirst);
            if (failures <= MaxRetries && nowUtc - last.StartedAt >= RetryDelay(failures))
                return (true, "Retry");
        }

        return (false, "Schedule");
    }

    /// <summary>A cron expression the scheduler can read, so a bad one is reported instead of silently never running.</summary>
    public static bool TryParse(string expression, out CronExpression cron)
    {
        try
        {
            cron = CronExpression.Parse(expression, CronFormat.Standard);
            return true;
        }
        catch (CronFormatException)
        {
            cron = null!;
            return false;
        }
    }
}

/// <summary>The delay before a failed queued job is tried again: 10 s, 20 s, 40 s ... up to one hour.</summary>
public static class RetryBackoff
{
    public static TimeSpan Delay(int attemptsSoFar) =>
        TimeSpan.FromSeconds(Math.Min(3600, 10 * Math.Pow(2, Math.Max(0, attemptsSoFar - 1))));
}
