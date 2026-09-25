using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Exports;
using MajdsApp.Modules.Files;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record ExportJobRow(Guid Id, string Source, string Title, string Format, string Status, Guid? FileId, string? FileName, long? Size, string? Error);
public record NotificationWithLink(int Id, string Title, string Message, string? Link);

/// <summary>F-Export FR-EXP-004: large exports run in the background, land in F-Files, and notify the user with a link.</summary>
public class BackgroundExportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<ExportJobRow> WaitForAsync(ApiClient client, Guid jobId, params string[] finished)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var job = (await client.GetAsync<List<ExportJobRow>>("/api/exports/list")).Data!.Single(j => j.Id == jobId);
            if (finished.Contains(job.Status)) return job;
            await Task.Delay(300);
        }

        throw new TimeoutException($"Export {jobId} did not reach {string.Join("/", finished)} in time.");
    }

    /// <summary>The notification is published just after the job's status changes, so wait for it rather than assume it is already there.</summary>
    private static async Task<List<NotificationWithLink>> WaitForNotificationAsync(ApiClient client, Func<NotificationWithLink, bool> match)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        List<NotificationWithLink> items;
        do
        {
            items = (await client.GetAsync<PagedData<NotificationWithLink>>("/api/notifications/list?page=1&pageSize=50")).Data!.Items;
            if (items.Any(match)) return items;
            await Task.Delay(200);
        }
        while (DateTime.UtcNow < deadline);

        return items;
    }

    private static Task<ApiResult<ExportJobRow>> StartAsync(ApiClient client, string source, string? format = "xlsx", Dictionary<string, string>? filters = null) =>
        client.PostAsync<ExportJobRow>("/api/exports/start", new { source, format, filters });

    private async Task<Guid> InsertJobAsync(string userId, ExportJobStatus status, string source = "users", DateTime? createdAt = null, Guid? fileId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = new ExportJob
        {
            Id = Guid.NewGuid(), UserId = userId, UserName = userId, Source = source, Title = "Users", Format = "csv", Filters = "{}",
            Status = status, FileId = fileId, CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Set<ExportJob>().Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    [Fact]
    public async Task A_started_export_runs_in_the_background_is_filtered_lands_in_Files_and_notifies_the_user_with_a_link()
    {
        var admin = await factory.SignInAsync("bg.admin1@example.com", "Admin");
        await factory.CreateUserAsync("bg.match@example.com");
        await factory.CreateUserAsync("bg.other@example.com");

        var started = await StartAsync(admin, "users", "xlsx", new() { ["filter"] = "bg.match" });

        started.Status.Should().Be(HttpStatusCode.OK);
        started.Data!.Status.Should().Be("Pending");                                     // returns straight away, work is queued (AC-EXP-3)

        var done = await WaitForAsync(admin, started.Data.Id, "Completed", "Failed");
        done.Status.Should().Be("Completed");
        done.FileName.Should().StartWith("users-").And.EndWith(".xlsx");

        var download = await admin.Http.GetAsync($"/api/exports/download?jobId={done.Id}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        using var workbook = new XLWorkbook(new MemoryStream(await download.Content.ReadAsByteArrayAsync()));
        workbook.Worksheet(1).RowsUsed().Skip(1).Select(r => r.Cell(1).GetString()).Should().Equal("bg.match@example.com");   // only matching rows (AC-EXP-1)

        var files = (await admin.GetAsync<PagedData<FileRow>>("/api/files/list?page=1&pageSize=50")).Data!.Items;
        files.Should().Contain(f => f.Id == done.FileId);                                // delivered through F-Files

        var notifications = await WaitForNotificationAsync(admin, n => n.Title == "Your export is ready");
        notifications.Should().Contain(n => n.Title == "Your export is ready" && n.Link == "/exports");
    }

    [Fact]
    public async Task A_CSV_background_export_works_for_the_audit_log_too()
    {
        var admin = await factory.SignInAsync("bg.admin2@example.com", "Admin");

        var started = await StartAsync(admin, "audit", "csv");
        var done = await WaitForAsync(admin, started.Data!.Id, "Completed", "Failed");

        done.Status.Should().Be("Completed");
        (await admin.Http.GetStringAsync($"/api/exports/download?jobId={done.Id}")).Should().Contain("When (UTC),Action");
    }

    [Fact]
    public async Task Starting_an_export_needs_the_permission_of_the_data_being_exported()
    {
        var plain = await factory.SignInAsync("bg.plain@example.com");

        var refused = await StartAsync(plain, "users");
        refused.Status.Should().Be(HttpStatusCode.Forbidden);
        refused.Body!.Message.Should().Contain("Users.View");
        (await StartAsync(factory.Anonymous(), "users")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_source_and_a_PDF_are_rejected()
    {
        var admin = await factory.SignInAsync("bg.admin3@example.com", "Admin");

        (await StartAsync(admin, "nope")).Status.Should().Be(HttpStatusCode.NotFound);
        var pdf = await StartAsync(admin, "users", "pdf");
        pdf.Status.Should().Be(HttpStatusCode.BadRequest);
        pdf.Errors.Should().Contain(e => e.Contains("2,000 rows"));
    }

    [Fact]
    public async Task Someone_elses_export_cannot_be_downloaded_and_an_unfinished_one_is_not_ready()
    {
        var owner = await factory.SignInAsync("bg.owner@example.com", "Admin");
        var other = await factory.SignInAsync("bg.stranger@example.com", "Admin");
        var ownerId = (await factory.CreateUserAsyncOrExisting("bg.owner@example.com")).Id;
        var running = await InsertJobAsync(ownerId, ExportJobStatus.Running);          // the worker only takes Pending jobs

        (await owner.Http.GetAsync($"/api/exports/download?jobId={running}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await other.Http.GetAsync($"/api/exports/download?jobId={running}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync<List<ExportJobRow>>("/api/exports/list")).Data!.Should().NotContain(j => j.Id == running);
    }

    [Fact]
    public async Task A_job_that_cannot_run_is_recorded_as_failed_and_the_user_is_told()
    {
        var admin = await factory.SignInAsync("bg.admin4@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bg.admin4@example.com")).Id;
        var job = await InsertJobAsync(adminId, ExportJobStatus.Pending, source: "vanished");

        var done = await WaitForAsync(admin, job, "Completed", "Failed");

        done.Status.Should().Be("Failed");
        done.Error.Should().Contain("no longer available");
        var notifications = await WaitForNotificationAsync(admin, n => n.Title == "Your export failed");
        notifications.Should().Contain(n => n.Title == "Your export failed");
    }

    [Fact]
    public async Task A_user_can_have_only_a_few_exports_in_progress_at_once()
    {
        var admin = await factory.SignInAsync("bg.admin5@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bg.admin5@example.com")).Id;
        for (var i = 0; i < 3; i++) await InsertJobAsync(adminId, ExportJobStatus.Running);

        var fourth = await StartAsync(admin, "users");

        fourth.Status.Should().Be(HttpStatusCode.Conflict);
        fourth.Body!.Message.Should().Contain("in progress");
    }

    [Fact]
    public async Task Jobs_interrupted_by_a_restart_are_put_back_in_the_queue()
    {
        var admin = await factory.SignInAsync("bg.admin6@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bg.admin6@example.com")).Id;
        await InsertJobAsync(adminId, ExportJobStatus.Running, source: "vanished");

        using var scope = factory.Services.CreateScope();
        var requeued = await scope.ServiceProvider.GetRequiredService<ExportJobProcessor>().RequeueInterruptedAsync(default);

        requeued.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Old_exports_and_their_files_are_removed_by_the_cleanup_job()
    {
        await factory.SignInAsync("bg.admin7@example.com", "Admin");
        var adminId = (await factory.CreateUserAsyncOrExisting("bg.admin7@example.com")).Id;

        Guid fileId;
        string storedName;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            storedName = Guid.NewGuid().ToString("N");
            using var content = new MemoryStream([1, 2, 3]);
            await scope.ServiceProvider.GetRequiredService<FileStorage>().SaveAsync(storedName, content, default);
            var record = new FileRecord { Id = Guid.NewGuid(), FileName = "old.csv", ContentType = "text/csv", Size = 3, StoredName = storedName, OwnerId = adminId, CreatedAt = DateTime.UtcNow };
            db.Set<FileRecord>().Add(record);
            await db.SaveChangesAsync();
            fileId = record.Id;
        }

        var oldJob = await InsertJobAsync(adminId, ExportJobStatus.Completed, createdAt: DateTime.UtcNow.AddDays(-30), fileId: fileId);
        var freshJob = await InsertJobAsync(adminId, ExportJobStatus.Completed, createdAt: DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ExportsCleanupJob>().ExecuteAsync(default);

        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await checkDb.Set<ExportJob>().AnyAsync(j => j.Id == oldJob)).Should().BeFalse();
        (await checkDb.Set<FileRecord>().AnyAsync(f => f.Id == fileId)).Should().BeFalse();
        (await checkDb.Set<ExportJob>().AnyAsync(j => j.Id == freshJob)).Should().BeTrue();
        ((Action)(() => check.ServiceProvider.GetRequiredService<FileStorage>().Open(storedName).Dispose())).Should().Throw<FileNotFoundException>();
    }
}

public record FileRow(Guid Id, string FileName);

internal static class FactoryExportExtensions
{
    /// <summary>The user (created by SignInAsync) looked up by email, to get its id for direct database setup.</summary>
    public static async Task<ApplicationUser> CreateUserAsyncOrExisting(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        return await users.FindByEmailAsync(email) ?? await factory.CreateUserAsync(email);
    }
}
