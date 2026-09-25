using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Files;
using MajdsApp.Modules.Jobs;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record QueuedJobRow(Guid Id, string Type, string Status, int Attempts, int MaxAttempts, string? UserName, string? LastError);

/// <summary>Jobs used only by these tests. Real modules register theirs the same way (an <see cref="IBackgroundJob"/>).</summary>
public static class TestJobs
{
    public static readonly ConcurrentDictionary<Guid, BackgroundJobContext> Ran = new();
    public static volatile bool ToggleBroken;

    public class Record : IBackgroundJob
    {
        public string Type => "test.record";
        public Task ExecuteAsync(BackgroundJobContext context, CancellationToken ct) { Ran[context.JobId] = context; return Task.CompletedTask; }
    }

    public class AlwaysFails : IBackgroundJob
    {
        public string Type => "test.fails";
        public int MaxAttempts => 2;
        public Task ExecuteAsync(BackgroundJobContext context, CancellationToken ct) => throw new InvalidOperationException("nope, always");
    }

    public class Flaky : IBackgroundJob
    {
        public string Type => "test.flaky";
        public Task ExecuteAsync(BackgroundJobContext context, CancellationToken ct)
        {
            if (context.Attempt < 3) throw new InvalidOperationException($"not yet (attempt {context.Attempt})");
            Ran[context.JobId] = context;
            return Task.CompletedTask;
        }
    }

    public class Toggle : IBackgroundJob
    {
        public string Type => "test.toggle";
        public int MaxAttempts => 1;
        public Task ExecuteAsync(BackgroundJobContext context, CancellationToken ct)
        {
            if (ToggleBroken) throw new InvalidOperationException("broken for now");
            Ran[context.JobId] = context;
            return Task.CompletedTask;
        }
    }

    public class FailingRecurring(string name) : IRecurringJob
    {
        public string Name => name;
        public TimeSpan Interval => TimeSpan.FromDays(1);
        public Task ExecuteAsync(CancellationToken ct) => throw new InvalidOperationException("recurring boom");
    }
}

/// <summary>A host whose worker is off, so a test drives each step itself and the outcome never races the clock.</summary>
public class JobsFactory : ApiFactory
{
    public JobsFactory() : base(new Dictionary<string, string> { ["Jobs:Worker:Enabled"] = "false" }, null)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IBackgroundJob, TestJobs.Record>();
            services.AddScoped<IBackgroundJob, TestJobs.AlwaysFails>();
            services.AddScoped<IBackgroundJob, TestJobs.Flaky>();
            services.AddScoped<IBackgroundJob, TestJobs.Toggle>();
        });
    }
}

/// <summary>F-Background-Jobs: the persisted queue, retries with backoff, restart recovery, user context and monitoring.</summary>
public class BackgroundJobTests(JobsFactory factory) : IClassFixture<JobsFactory>
{
    private async Task<Guid> EnqueueAsync(string type, string? userId = "user-1", string? userName = "alice", Dictionary<string, string>? parameters = null, TimeSpan? delay = null)
    {
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = userId is null ? null : new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userName ?? "")], "test"))
        };

        return await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>().EnqueueAsync(type, parameters, delay);
    }

    private async Task DrainAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            using var scope = factory.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ProcessNextAsync(default)) return;
        }
    }

    private async Task<BackgroundJob> GetAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().AsNoTracking().FirstAsync(j => j.Id == id);
    }

    private async Task MakeDueAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)));
    }

    // ---- run, delay and user context ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_queued_job_runs_with_its_parameters_and_the_identity_of_the_user_who_queued_it()
    {
        var id = await EnqueueAsync("test.record", "user-42", "alice@example.com", new() { ["orderId"] = "1001" });

        (await GetAsync(id)).Status.Should().Be(BackgroundJobStatus.Pending);
        await DrainAsync();

        var job = await GetAsync(id);
        job.Status.Should().Be(BackgroundJobStatus.Succeeded);
        job.Attempts.Should().Be(1);
        job.CompletedAt.Should().NotBeNull();
        TestJobs.Ran[id].Should().Match<BackgroundJobContext>(c => c.UserId == "user-42" && c.UserName == "alice@example.com"
            && c.Parameters["orderId"] == "1001" && c.Attempt == 1);                                     // FR-JOB-004
    }

    [Fact]
    public async Task A_job_queued_with_no_user_runs_without_one()
    {
        var id = await EnqueueAsync("test.record", userId: null);

        await DrainAsync();

        TestJobs.Ran[id].UserId.Should().BeNull();
    }

    [Fact]
    public async Task A_delayed_job_does_not_run_until_it_is_due()
    {
        var id = await EnqueueAsync("test.record", delay: TimeSpan.FromHours(1));

        await DrainAsync();
        (await GetAsync(id)).Status.Should().Be(BackgroundJobStatus.Pending);

        await MakeDueAsync(id);
        await DrainAsync();
        (await GetAsync(id)).Status.Should().Be(BackgroundJobStatus.Succeeded);
    }

    [Fact]
    public async Task An_unknown_job_type_and_oversized_parameters_are_refused_when_queueing()
    {
        await factory.Awaiting(f => EnqueueAsync("nope.nothing")).Should().ThrowAsync<ArgumentException>().WithMessage("*no background job of type*");
        await factory.Awaiting(f => EnqueueAsync("test.record", parameters: new() { ["big"] = new string('x', BackgroundJobQueue.MaxParametersLength) }))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*larger than*");
    }

    // ---- retry with backoff (AC-JOB-2) ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_failing_job_is_retried_with_a_delay_then_left_failed_and_the_administrators_are_told()
    {
        var admin = await factory.SignInAsync("jobs.admin1@example.com", "Admin");
        var id = await EnqueueAsync("test.fails");

        await DrainAsync();                                                                            // attempt 1 fails
        var afterFirst = await GetAsync(id);
        afterFirst.Status.Should().Be(BackgroundJobStatus.Pending);
        afterFirst.Attempts.Should().Be(1);
        afterFirst.LastError.Should().Contain("nope, always");
        afterFirst.NextAttemptAt.Should().BeAfter(DateTime.UtcNow.AddSeconds(5));                        // backing off, not immediate

        await DrainAsync();
        (await GetAsync(id)).Attempts.Should().Be(1);                                                  // not due yet: nothing ran

        await MakeDueAsync(id);
        await DrainAsync();                                                                            // attempt 2 fails: retries used up
        var final = await GetAsync(id);
        final.Status.Should().Be(BackgroundJobStatus.Failed);
        final.Attempts.Should().Be(2);
        final.CompletedAt.Should().NotBeNull();

        var notes = (await admin.GetAsync<PagedData<NotificationWithLink>>("/api/notifications/list?page=1&pageSize=50")).Data!.Items;
        notes.Should().Contain(n => n.Title == "Background job failed" && n.Message.Contains("test.fails") && n.Message.Contains("2 attempts"));
    }

    [Fact]
    public async Task A_job_that_fails_at_first_but_recovers_succeeds_on_a_later_attempt()
    {
        var id = await EnqueueAsync("test.flaky");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await DrainAsync();
            if (attempt < 3) await MakeDueAsync(id);
        }

        var job = await GetAsync(id);
        job.Status.Should().Be(BackgroundJobStatus.Succeeded);
        job.Attempts.Should().Be(3);
        TestJobs.Ran[id].Attempt.Should().Be(3);
    }

    // ---- restart (AC-JOB-1) -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_job_that_was_running_when_the_process_stopped_is_run_again_after_the_restart()
    {
        var id = await EnqueueAsync("test.record");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().Where(j => j.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BackgroundJobStatus.Running).SetProperty(j => j.Attempts, 1));

        using (var scope = factory.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().RequeueInterruptedAsync(default)).Should().BeGreaterThanOrEqualTo(1);

        (await GetAsync(id)).Status.Should().Be(BackgroundJobStatus.Pending);
        await DrainAsync();
        (await GetAsync(id)).Status.Should().Be(BackgroundJobStatus.Succeeded);
    }

    [Fact]
    public async Task A_job_that_keeps_crashing_the_process_ends_up_failed_instead_of_looping_forever()
    {
        var id = await EnqueueAsync("test.fails");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().Where(j => j.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BackgroundJobStatus.Running).SetProperty(j => j.Attempts, 2));   // all attempts used

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().RequeueInterruptedAsync(default);

        var job = await GetAsync(id);
        job.Status.Should().Be(BackgroundJobStatus.Failed);
        job.LastError.Should().Contain("Interrupted by a restart");
    }

    // ---- monitoring: view, retry, delete (FR-JOB-003, AC-JOB-3) --------------------------------------------------------------

    [Fact]
    public async Task An_administrator_can_see_filter_retry_and_delete_queued_jobs()
    {
        var admin = await factory.SignInAsync("jobs.admin2@example.com", "Admin");
        TestJobs.ToggleBroken = true;
        var failed = await EnqueueAsync("test.toggle", userName: "bob");
        var waiting = await EnqueueAsync("test.record", delay: TimeSpan.FromHours(2));
        await DrainAsync();                                                                            // toggle fails: 1 attempt is all it has

        var onlyFailed = (await admin.GetAsync<PagedData<QueuedJobRow>>("/api/jobs/queue?page=1&pageSize=50&status=Failed")).Data!.Items;
        onlyFailed.Should().Contain(j => j.Id == failed && j.LastError!.Contains("broken for now") && j.UserName == "bob");
        onlyFailed.Should().NotContain(j => j.Id == waiting);
        (await admin.GetAsync<PagedData<QueuedJobRow>>("/api/jobs/queue?page=1&pageSize=50&status=Pending")).Data!.Items.Should().Contain(j => j.Id == waiting);
        (await admin.GetAsync<PagedData<QueuedJobRow>>("/api/jobs/queue?page=1&pageSize=50&status=Banana")).Status.Should().Be(HttpStatusCode.BadRequest);

        TestJobs.ToggleBroken = false;
        (await admin.PostAsync("/api/jobs/retry", new { jobId = failed })).Status.Should().Be(HttpStatusCode.OK);
        (await GetAsync(failed)).Should().Match<BackgroundJob>(j => j.Status == BackgroundJobStatus.Pending && j.Attempts == 0);
        await DrainAsync();
        (await GetAsync(failed)).Status.Should().Be(BackgroundJobStatus.Succeeded);

        (await admin.PostAsync("/api/jobs/retry", new { jobId = failed })).Status.Should().Be(HttpStatusCode.Conflict);   // only failed jobs
        (await admin.PostAsync("/api/jobs/delete", new { jobId = waiting })).Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<PagedData<QueuedJobRow>>("/api/jobs/queue?page=1&pageSize=50")).Data!.Items.Should().NotContain(j => j.Id == waiting);
        (await admin.PostAsync("/api/jobs/delete", new { jobId = waiting })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_running_job_cannot_be_deleted()
    {
        var admin = await factory.SignInAsync("jobs.admin3@example.com", "Admin");
        var id = await EnqueueAsync("test.record");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().Where(j => j.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BackgroundJobStatus.Running));

        var response = await admin.PostAsync("/api/jobs/delete", new { jobId = id });

        response.Status.Should().Be(HttpStatusCode.Conflict);
        using var cleanup = factory.Services.CreateScope();
        await cleanup.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().Where(j => j.Id == id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task The_monitoring_endpoints_need_permission()
    {
        var plain = await factory.SignInAsync("jobs.plain@example.com");
        var id = Guid.NewGuid();

        (await plain.GetAsync<PagedData<QueuedJobRow>>("/api/jobs/queue?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.PostAsync("/api/jobs/retry", new { jobId = id })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.PostAsync("/api/jobs/delete", new { jobId = id })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await factory.Anonymous().GetAsync<PagedData<QueuedJobRow>>("/api/jobs/queue")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- recurring jobs: retry policy and who gets told -----------------------------------------------------------------------

    [Fact]
    public async Task A_failing_scheduled_job_only_alerts_the_administrators_once_its_retries_are_used_up()
    {
        var admin = await factory.SignInAsync("jobs.admin4@example.com", "Admin");
        var job = new TestJobs.FailingRecurring("test.recurring.scheduled");

        async Task FailOnceAsync(string trigger)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<JobExecutor>().RunAsync(job, trigger, default);
        }

        async Task<int> AlertsAsync() =>
            (await admin.GetAsync<PagedData<NotificationWithLink>>("/api/notifications/list?page=1&pageSize=100")).Data!.Items
                .Count(n => n.Title == "Background job failed" && n.Message.Contains(job.Name));

        await FailOnceAsync("Schedule");
        for (var retry = 1; retry < JobSchedule.MaxRetries; retry++) await FailOnceAsync("Retry");
        (await AlertsAsync()).Should().Be(0);                                                          // still retrying: nobody is bothered

        await FailOnceAsync("Retry");                                                                  // the last allowed retry fails too
        (await AlertsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_job_run_by_hand_that_fails_alerts_straight_away()
    {
        var admin = await factory.SignInAsync("jobs.admin5@example.com", "Admin");
        var job = new TestJobs.FailingRecurring("test.recurring.manual");

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<JobExecutor>().RunAsync(job, "Manual", default);

        (await admin.GetAsync<PagedData<NotificationWithLink>>("/api/notifications/list?page=1&pageSize=100")).Data!.Items
            .Should().Contain(n => n.Title == "Background job failed" && n.Message.Contains(job.Name));
    }

    // ---- temp-file cleanup (FR-JOB-005) ---------------------------------------------------------------------------------------

    private sealed class TempEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task Stored_files_nothing_points_to_are_removed_after_a_day_and_everything_else_is_kept()
    {
        // A temporary storage folder, never the API project's real App_Data.
        var contentRoot = Path.Combine(Path.GetTempPath(), "majds-orphans-" + Guid.NewGuid().ToString("N"));
        var storage = new FileStorage(new TempEnvironment(contentRoot));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        async Task<string> StoreAsync(string name, TimeSpan age)
        {
            await storage.SaveAsync(name, new MemoryStream([1, 2, 3]), default);
            File.SetLastWriteTimeUtc(Path.Combine(contentRoot, "App_Data", "files", name), DateTime.UtcNow - age);
            return name;
        }

        var recorded = await StoreAsync("recorded-old", TimeSpan.FromDays(3));
        var orphanOld = await StoreAsync("orphan-old", TimeSpan.FromDays(3));
        var orphanNew = await StoreAsync("orphan-new", TimeSpan.FromMinutes(5));
        db.Set<FileRecord>().Add(new FileRecord { Id = Guid.NewGuid(), FileName = "x", ContentType = "text/plain", Size = 3, StoredName = recorded, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        try
        {
            await new OrphanedFilesCleanupJob(db, storage).ExecuteAsync(default);

            var left = storage.Enumerate().Select(f => f.Name).ToList();
            left.Should().BeEquivalentTo(recorded, orphanNew);                                          // only the old orphan is gone
            left.Should().NotContain(orphanOld);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}

/// <summary>The hosted worker, running for real: a queued job is picked up and finished without anyone driving it.</summary>
public class BackgroundJobWorkerTests(BackgroundJobWorkerTests.WorkerFactory factory) : IClassFixture<BackgroundJobWorkerTests.WorkerFactory>
{
    public class WorkerFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddScoped<IBackgroundJob, TestJobs.Record>());
        }
    }

    [Fact]
    public async Task The_worker_picks_up_a_queued_job_and_completes_it()
    {
        Guid id;
        using (var scope = factory.Services.CreateScope())
            id = await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>().EnqueueAsync("test.record", new Dictionary<string, string> { ["k"] = "v" });

        var deadline = DateTime.UtcNow.AddSeconds(20);
        BackgroundJob? job = null;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory.Services.CreateScope();
            job = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BackgroundJob>().AsNoTracking().FirstAsync(j => j.Id == id);
            if (job.Status == BackgroundJobStatus.Succeeded) break;
            await Task.Delay(300);
        }

        job!.Status.Should().Be(BackgroundJobStatus.Succeeded);
        TestJobs.Ran[id].Parameters["k"].Should().Be("v");
    }
}
