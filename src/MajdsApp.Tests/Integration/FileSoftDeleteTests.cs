using System.Net;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Settings;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Files FR-FILE-006: deleting hides a file at once and a background job removes it physically after the retention period.</summary>
public class FileSoftDeleteTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record FileRow(Guid Id, string FileName);

    private sealed class TempEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private async Task<Guid> SeedAsync(string name, string stored, DateTime? deletedAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid();
        db.Set<FileRecord>().Add(new FileRecord { Id = id, FileName = name, ContentType = "text/plain", Size = 3, StoredName = stored, CreatedAt = DateTime.UtcNow, DeletedAt = deletedAt });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<List<Guid>> ListedAsync(ApiClient client) =>
        (await client.GetAsync<PagedData<FileRow>>("/api/files/list?page=1&pageSize=200")).Data!.Items.Select(f => f.Id).ToList();

    [Fact]
    public async Task A_deleted_file_disappears_at_once_but_its_record_is_kept_for_the_cleanup_job()
    {
        var admin = await factory.SignInAsync("fsd.admin1@example.com", "Admin");
        var id = await SeedAsync("soft-1.txt", "stored-soft-1");
        (await ListedAsync(admin)).Should().Contain(id);

        (await admin.PostAsync("/api/files/delete", new { fileId = id })).Status.Should().Be(HttpStatusCode.OK);

        (await ListedAsync(admin)).Should().NotContain(id);
        (await admin.Http.GetAsync($"/api/files/download?fileId={id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync("/api/files/delete", new { fileId = id })).Status.Should().Be(HttpStatusCode.NotFound); // already gone
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<FileRecord>().IgnoreQueryFilters().SingleAsync(f => f.Id == id);
        row.DeletedAt.Should().NotBeNull();
        row.DeletedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_cleanup_job_removes_bytes_and_record_only_after_the_retention_period()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "majds-softdelete-" + Guid.NewGuid().ToString("N"));
        var storage = new FileStorage(new TempEnvironment(contentRoot));
        try
        {
            await storage.SaveAsync("old-deleted", new MemoryStream([1]), default);
            await storage.SaveAsync("recent-deleted", new MemoryStream([1]), default);
            await storage.SaveAsync("live", new MemoryStream([1]), default);
            var oldId = await SeedAsync("old.txt", "old-deleted", DateTime.UtcNow.AddDays(-31));
            var recentId = await SeedAsync("recent.txt", "recent-deleted", DateTime.UtcNow.AddDays(-2));
            var liveId = await SeedAsync("live.txt", "live");

            using (var scope = factory.Services.CreateScope())
                await new PurgeDeletedFilesJob(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), storage, scope.ServiceProvider.GetRequiredService<ISettingsProvider>()).ExecuteAsync(default);

            using var check = factory.Services.CreateScope();
            var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var remaining = await db.Set<FileRecord>().IgnoreQueryFilters().Select(f => f.Id).ToListAsync();
            remaining.Should().NotContain(oldId).And.Contain([recentId, liveId]);
            storage.Enumerate().Select(f => f.Name).Should().BeEquivalentTo("recent-deleted", "live");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task The_orphan_sweep_does_not_treat_a_soft_deleted_files_bytes_as_orphaned()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "majds-softdelete2-" + Guid.NewGuid().ToString("N"));
        var storage = new FileStorage(new TempEnvironment(contentRoot));
        try
        {
            await storage.SaveAsync("kept-until-purge", new MemoryStream([1]), default);
            File.SetLastWriteTimeUtc(Path.Combine(contentRoot, "App_Data", "files", "kept-until-purge"), DateTime.UtcNow.AddDays(-5));
            await SeedAsync("deleted.txt", "kept-until-purge", DateTime.UtcNow.AddDays(-1));

            using var scope = factory.Services.CreateScope();
            await new OrphanedFilesCleanupJob(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), storage).ExecuteAsync(default);

            storage.Enumerate().Select(f => f.Name).Should().Contain("kept-until-purge");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task The_retention_period_is_a_setting_with_a_valid_range()
    {
        var admin = await factory.SignInAsync("fsd.admin2@example.com", "Admin");

        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Files.DeletedRetentionDays", value = "0" } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Files.DeletedRetentionDays", value = "14" } } })).Status.Should().Be(HttpStatusCode.OK);
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Files.DeletedRetentionDays", value = "30" } } });
    }
}
