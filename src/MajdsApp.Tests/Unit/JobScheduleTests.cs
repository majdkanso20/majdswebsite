using FluentAssertions;
using MajdsApp.Modules.Files;
using MajdsApp.Modules.Jobs;
using MajdsApp.Modules.Plugins;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Plugins;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class JobScheduleTests
{
    private sealed class Job(TimeSpan interval, string? cron = null) : IRecurringJob
    {
        public string Name => "test";
        public TimeSpan Interval => interval;
        public string? Cron => cron;
        public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly DateTime Noon = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private static JobRun Run(DateTime startedAt, bool success) => new() { JobName = "test", StartedAt = startedAt, Success = success };

    // ---- interval schedule ---------------------------------------------------------------------------------------

    [Fact]
    public void A_job_that_has_never_run_is_due_at_once()
    {
        JobSchedule.Evaluate(new Job(TimeSpan.FromHours(1)), [], Noon).Should().Be((true, "Schedule"));
    }

    [Fact]
    public void A_job_is_due_once_its_interval_has_passed_and_not_before()
    {
        var job = new Job(TimeSpan.FromHours(1));
        var runs = new[] { Run(Noon.AddMinutes(-59), success: true) };

        JobSchedule.Evaluate(job, runs, Noon).Due.Should().BeFalse();
        JobSchedule.Evaluate(job, runs, Noon.AddMinutes(1)).Should().Be((true, "Schedule"));
    }

    // ---- retry after failure (AC-JOB-2) ---------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    public void Retries_wait_longer_each_time(int failures, int minutes) =>
        JobSchedule.RetryDelay(failures).Should().Be(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void A_failed_run_is_retried_soon_instead_of_waiting_a_whole_interval()
    {
        var job = new Job(TimeSpan.FromHours(24));
        var runs = new[] { Run(Noon.AddSeconds(-30), success: false) };

        JobSchedule.Evaluate(job, runs, Noon).Due.Should().BeFalse();                           // the first retry waits a minute
        JobSchedule.Evaluate(job, runs, Noon.AddSeconds(31)).Should().Be((true, "Retry"));
    }

    [Fact]
    public void Retries_stop_after_the_limit_and_the_job_waits_for_its_next_scheduled_time()
    {
        var job = new Job(TimeSpan.FromHours(24));
        // Original failure plus MaxRetries retries, all failed: nothing more until the interval passes.
        var runs = Enumerable.Range(0, JobSchedule.MaxRetries + 1).Select(i => Run(Noon.AddMinutes(-(10 + i)), success: false)).ToList();

        JobSchedule.ConsecutiveFailures(runs).Should().Be(JobSchedule.MaxRetries + 1);
        JobSchedule.Evaluate(job, runs, Noon.AddHours(2)).Due.Should().BeFalse();
        JobSchedule.Evaluate(job, runs, Noon.AddHours(24)).Should().Be((true, "Schedule"));
    }

    [Fact]
    public void A_success_ends_the_failure_streak()
    {
        var runs = new[] { Run(Noon, true), Run(Noon.AddMinutes(-5), false), Run(Noon.AddMinutes(-10), false) };

        JobSchedule.ConsecutiveFailures(runs).Should().Be(0);
        JobSchedule.ConsecutiveFailures([Run(Noon, false), Run(Noon.AddMinutes(-5), false), Run(Noon.AddMinutes(-10), true)]).Should().Be(2);
    }

    [Fact]
    public void The_next_run_shown_is_the_retry_when_that_comes_sooner()
    {
        var job = new Job(TimeSpan.FromHours(24));
        var failed = new[] { Run(Noon, success: false) };
        var ok = new[] { Run(Noon, success: true) };

        JobSchedule.NextRun(job, failed).Should().Be(Noon.AddMinutes(1));
        JobSchedule.NextRun(job, ok).Should().Be(Noon.AddHours(24));
        JobSchedule.NextRun(job, []).Should().BeNull();
    }

    // ---- cron (FR-JOB-001) -----------------------------------------------------------------------------------------

    [Fact]
    public void A_cron_job_runs_at_its_next_cron_time_not_after_a_fixed_interval()
    {
        var job = new Job(TimeSpan.FromMinutes(1), cron: "0 3 * * *"); // every day at 03:00 UTC
        var lastRun = new DateTime(2026, 9, 24, 3, 0, 5, DateTimeKind.Utc);
        var runs = new[] { Run(lastRun, success: true) };

        JobSchedule.NextRegularRun(job, lastRun).Should().Be(new DateTime(2026, 9, 25, 3, 0, 0, DateTimeKind.Utc));
        JobSchedule.Evaluate(job, runs, new DateTime(2026, 9, 25, 2, 59, 0, DateTimeKind.Utc)).Due.Should().BeFalse();
        JobSchedule.Evaluate(job, runs, new DateTime(2026, 9, 25, 3, 0, 0, DateTimeKind.Utc)).Should().Be((true, "Schedule"));
    }

    [Fact]
    public void A_cron_expression_that_cannot_be_read_falls_back_to_the_interval_and_is_recognised_as_invalid()
    {
        var job = new Job(TimeSpan.FromHours(1), cron: "not a cron");

        JobSchedule.TryParse("not a cron", out _).Should().BeFalse();
        JobSchedule.TryParse("*/15 * * * *", out _).Should().BeTrue();
        JobSchedule.NextRegularRun(job, Noon).Should().Be(Noon.AddHours(1));
    }

    // ---- queued job backoff -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(20, 3600)]
    public void A_failed_queued_job_waits_longer_before_each_retry_up_to_an_hour(int attemptsSoFar, int seconds) =>
        RetryBackoff.Delay(attemptsSoFar).Should().Be(TimeSpan.FromSeconds(seconds));

    // ---- temp-file cleanup (FR-JOB-005) -----------------------------------------------------------------------------

    [Fact]
    public async Task Plugin_staging_folders_left_behind_for_a_day_are_removed_and_recent_ones_are_kept()
    {
        var root = Path.Combine(Path.GetTempPath(), "majds-staging-" + Guid.NewGuid().ToString("N"));
        var old = Path.Combine(root, ".staging", "old");
        var fresh = Path.Combine(root, ".staging", "fresh");
        var pending = Path.Combine(root, ".pending", "Keep.Me");
        foreach (var dir in new[] { old, fresh, pending }) Directory.CreateDirectory(dir);
        Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));
        Directory.SetLastWriteTimeUtc(pending, DateTime.UtcNow.AddDays(-3));

        try
        {
            await new PluginStagingCleanupJob(new PluginHostOptions(root, false, [])).ExecuteAsync(default);

            Directory.Exists(old).Should().BeFalse();
            Directory.Exists(fresh).Should().BeTrue();
            Directory.Exists(pending).Should().BeTrue();                                    // staged changes are never scratch space
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
