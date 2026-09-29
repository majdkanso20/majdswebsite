using System.Net;
using System.Text;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Imports;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record ImportJobNotificationRow(int Id, string Title, string Message, string? Link);

/// <summary>F-Export FR-EXP-003/004: the import job queue itself — permission, an unknown source, a per-user
/// cap, restart recovery, and cleanup — the counterpart to <see cref="BackgroundExportTests"/>.</summary>
public class BackgroundImportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<Guid> InsertJobAsync(string userId, ImportJobStatus status, string source = "users", DateTime? createdAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = new ImportJob
        {
            Id = Guid.NewGuid(), UserId = userId, UserName = userId, Source = source, Title = "Users", FileName = "x.csv",
            Content = Encoding.UTF8.GetBytes("Email\r\na@b.co\r\n"), Status = status, CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Set<ImportJob>().Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private static async Task<ImportJobRow> WaitForAsync(ApiClient client, Guid jobId, params string[] finished)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var job = (await client.GetAsync<List<ImportJobRow>>("/api/imports/list")).Data!.Single(j => j.Id == jobId);
            if (finished.Contains(job.Status)) return job;
            await Task.Delay(300);
        }

        throw new TimeoutException($"Import {jobId} did not reach {string.Join("/", finished)} in time.");
    }

    private static async Task<List<ImportJobNotificationRow>> WaitForNotificationAsync(ApiClient client, Func<ImportJobNotificationRow, bool> match)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        List<ImportJobNotificationRow> items;
        do
        {
            items = (await client.GetAsync<PagedData<ImportJobNotificationRow>>("/api/notifications/list?page=1&pageSize=50")).Data!.Items;
            if (items.Any(match)) return items;
            await Task.Delay(200);
        }
        while (DateTime.UtcNow < deadline);

        return items;
    }

    [Fact]
    public async Task An_unknown_source_is_a_404_and_never_reaches_the_queue()
    {
        var admin = await factory.SignInAsync("bgi.admin1@example.com", "Admin");
        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("x")), "file", "x.csv" } };

        var response = await admin.Http.PostAsync("/api/imports/start?source=nope", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_job_whose_source_vanished_before_it_ran_is_recorded_as_failed_and_the_user_is_told()
    {
        var admin = await factory.SignInAsync("bgi.admin2@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bgi.admin2@example.com")).Id;
        var job = await InsertJobAsync(adminId, ImportJobStatus.Pending, source: "vanished");

        var done = await WaitForAsync(admin, job, "Completed", "Failed");

        done.Status.Should().Be("Failed");
        done.Error.Should().Contain("no longer available");
        var notifications = await WaitForNotificationAsync(admin, n => n.Title.Contains("import failed"));
        notifications.Should().Contain(n => n.Link == "/imports");
    }

    [Fact]
    public async Task A_user_can_have_only_a_few_imports_in_progress_at_once()
    {
        var admin = await factory.SignInAsync("bgi.admin3@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bgi.admin3@example.com")).Id;
        for (var i = 0; i < 3; i++) await InsertJobAsync(adminId, ImportJobStatus.Running);

        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("Email\r\na@b.co\r\n")), "file", "x.csv" } };
        var response = await admin.Http.PostAsync("/api/imports/start?source=users", form);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("in progress");
    }

    [Fact]
    public async Task Jobs_interrupted_by_a_restart_are_put_back_in_the_queue()
    {
        var admin = await factory.SignInAsync("bgi.admin4@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bgi.admin4@example.com")).Id;
        await InsertJobAsync(adminId, ImportJobStatus.Running, source: "vanished");

        using var scope = factory.Services.CreateScope();
        var requeued = await scope.ServiceProvider.GetRequiredService<ImportJobProcessor>().RequeueInterruptedAsync(default);

        requeued.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Old_finished_imports_are_removed_by_the_cleanup_job_but_a_fresh_one_is_kept()
    {
        await factory.SignInAsync("bgi.admin5@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bgi.admin5@example.com")).Id;

        var oldJob = await InsertJobAsync(adminId, ImportJobStatus.Completed, createdAt: DateTime.UtcNow.AddDays(-60));
        var freshJob = await InsertJobAsync(adminId, ImportJobStatus.Completed, createdAt: DateTime.UtcNow);
        var stillRunning = await InsertJobAsync(adminId, ImportJobStatus.Running, createdAt: DateTime.UtcNow.AddDays(-60));

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ImportsCleanupJob>().ExecuteAsync(default);

        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<ImportJob>().AnyAsync(j => j.Id == oldJob)).Should().BeFalse();
        (await db.Set<ImportJob>().AnyAsync(j => j.Id == freshJob)).Should().BeTrue();
        (await db.Set<ImportJob>().AnyAsync(j => j.Id == stillRunning)).Should().BeTrue(); // never cleaned up mid-flight, however old
    }
}
