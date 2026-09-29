using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Plugins;

public record LifecyclePluginRow(
    string Id, string Version, bool IsEnabled, List<string> Permissions, List<string> MenuEntries, bool CanRollback, bool PendingUninstall);
public record PendingRow(string Id, string Version, string Action);
public record ChangeRow(string Id, string Version, string Action, string? PreviousVersion, string Sha256, bool RestartRequired);

internal static class PluginApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<(HttpStatusCode Status, Envelope<ChangeRow>? Body)> InstallAsync(ApiClient client, byte[] package, string? sha256 = null, string fileName = "plugin.zip")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(package);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", fileName);
        if (sha256 is not null) form.Add(new StringContent(sha256), "sha256");

        var response = await client.Http.PostAsync("/api/plugins/install", form);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.StartsWith('{') ? JsonSerializer.Deserialize<Envelope<ChangeRow>>(text, Json) : null);
    }

    public static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

/// <summary>P5 install, upgrade and rollback through the admin API on a host that already runs the sample plugin.</summary>
[Collection(PluginHostCollection.Name)]
public class PluginInstallApiTests(PluginHostFactory factory)
{
    private const string Id = "MajdsApp.Plugins.Tasks";

    [Fact]
    public async Task An_upload_of_a_newer_version_is_verified_and_staged_for_the_next_start_leaving_the_running_plugin_alone()
    {
        var admin = await factory.SignInAsync("pl.admin1@example.com", "Admin");
        var package = PluginPackages.Create(m => m["version"] = "1.1.0");

        var (status, body) = await PluginApi.InstallAsync(admin, package, PluginApi.Sha(package));

        status.Should().Be(HttpStatusCode.OK);
        body!.Data.Should().Match<ChangeRow>(c => c.Id == Id && c.Action == "Upgrade" && c.Version == "1.1.0" && c.PreviousVersion == "1.0.1"
            && c.RestartRequired && c.Sha256 == PluginApi.Sha(package));

        (await admin.GetAsync<List<PendingRow>>("/api/plugins/pending")).Data!.Should().ContainSingle(p => p.Id == Id && p.Action == "Upgrade");
        (await admin.GetAsync<PagedData<TaskRow>>("/api/tasks/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.OK);   // still running
        (await admin.GetAsync<List<LifecyclePluginRow>>("/api/plugins/list")).Data!.Single(p => p.Id == Id).Version.Should().Be("1.0.1");

        (await admin.PostAsync("/api/plugins/cancel-pending", new { pluginId = Id })).Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<List<PendingRow>>("/api/plugins/pending")).Data!.Should().BeEmpty();
        (await admin.PostAsync("/api/plugins/cancel-pending", new { pluginId = Id })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uploads_that_cannot_be_installed_are_refused_with_the_reason()
    {
        var admin = await factory.SignInAsync("pl.admin2@example.com", "Admin");

        var same = await PluginApi.InstallAsync(admin, PluginPackages.Create());
        same.Status.Should().Be(HttpStatusCode.BadRequest);
        same.Body!.Errors.Should().Contain(e => e.Contains("already installed"));

        var notZip = await PluginApi.InstallAsync(admin, [1, 2, 3, 4]);
        notZip.Status.Should().Be(HttpStatusCode.BadRequest);
        notZip.Body!.Errors.Should().Contain(e => e.Contains("not a valid .zip"));

        var wrongChecksum = await PluginApi.InstallAsync(admin, PluginPackages.Create(m => m["version"] = "1.5.0"), new string('a', 64));
        wrongChecksum.Status.Should().Be(HttpStatusCode.BadRequest);
        wrongChecksum.Body!.Errors.Should().Contain(e => e.Contains("checksum"));

        var incompatible = await PluginApi.InstallAsync(admin, PluginPackages.Create(m => { m["version"] = "1.6.0"; m["minHostVersion"] = "99.0.0"; }));
        incompatible.Status.Should().Be(HttpStatusCode.BadRequest);
        incompatible.Body!.Errors.Should().Contain(e => e.Contains("platform version"));

        (await admin.GetAsync<List<PendingRow>>("/api/plugins/pending")).Data!.Should().BeEmpty();   // every refusal left nothing staged
    }

    [Fact]
    public async Task Rolling_back_with_no_earlier_version_is_a_clear_error()
    {
        var admin = await factory.SignInAsync("pl.admin3@example.com", "Admin");

        var response = await admin.PostAsync("/api/plugins/rollback", new { pluginId = Id });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        response.Errors.Should().Contain(e => e.Contains("no earlier version"));
    }

    [Fact]
    public async Task Installing_needs_the_manage_permission()
    {
        var plain = await factory.SignInAsync("pl.plain@example.com");
        var package = PluginPackages.Create(m => m["version"] = "1.9.0");

        (await PluginApi.InstallAsync(plain, package)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await PluginApi.InstallAsync(factory.Anonymous(), package)).Status.Should().Be(HttpStatusCode.Unauthorized);
        (await plain.PostAsync("/api/plugins/uninstall", new { pluginId = Id })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.PostAsync("/api/plugins/rollback", new { pluginId = Id })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_plugin_list_shows_its_declared_permissions_and_menu_entries()
    {
        var admin = await factory.SignInAsync("pl.admin4@example.com", "Admin");

        var plugin = (await admin.GetAsync<List<LifecyclePluginRow>>("/api/plugins/list")).Data!.Single(p => p.Id == Id);

        plugin.Permissions.Should().BeEquivalentTo("Tasks.View", "Tasks.Create", "Tasks.Edit", "Tasks.Delete");
        plugin.MenuEntries.Should().Contain("Tasks");
        plugin.CanRollback.Should().BeFalse();
    }
}

/// <summary>Uninstalling: it stops at once, its permissions are cleaned up everywhere, its files go at the next start.</summary>
[Collection(PluginHostCollection.Name)]
public class PluginUninstallApiTests(PluginHostFactory factory)
{
    private const string Id = "MajdsApp.Plugins.Tasks";

    [Fact]
    public async Task Uninstalling_stops_the_plugin_removes_its_grants_and_leaves_other_permissions_alone()
    {
        var admin = await factory.SignInAsync("un.admin@example.com", "Admin");
        var holder = await factory.CreateUserAsync("un.holder@example.com");

        string roleId;
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var role = new ApplicationRole { Name = "Task people" };
            (await roles.CreateAsync(role)).Succeeded.Should().BeTrue();
            roleId = role.Id;

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<RolePermission>().AddRange(
                new RolePermission { RoleId = roleId, PermissionName = "Tasks.View", IsGranted = true },
                new RolePermission { RoleId = roleId, PermissionName = "Tasks.Create", IsGranted = true },
                new RolePermission { RoleId = roleId, PermissionName = "Users.View", IsGranted = true });
            db.Set<UserPermission>().AddRange(
                new UserPermission { UserId = holder.Id, PermissionName = "Tasks.Edit", IsGranted = true },
                new UserPermission { UserId = holder.Id, PermissionName = "Audit.View", IsGranted = true });
            await db.SaveChangesAsync();
        }

        try
        {
        var response = await admin.PostAsync("/api/plugins/uninstall", new { pluginId = Id });

        response.Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<List<LifecyclePluginRow>>("/api/plugins/list")).Data!.Should().NotContain(p => p.Id == Id);   // registry entry gone
        (await admin.GetAsync<List<PendingRow>>("/api/plugins/pending")).Data!.Should().ContainSingle(p => p.Id == Id && p.Action == "Uninstall");

        var blocked = await admin.GetAsync<PagedData<TaskRow>>("/api/tasks/list?page=1&pageSize=5");                          // stops immediately
        blocked.Status.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync<JsonElement>("/api/plugins/manifest")).Data.GetArrayLength().Should().Be(0);                    // menu entry gone

        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        checkDb.Set<RolePermission>().Where(rp => rp.RoleId == roleId).Select(rp => rp.PermissionName).ToList()
            .Should().BeEquivalentTo("Users.View");                                                                            // no orphaned grants (FR-PLUG-028)
        checkDb.Set<UserPermission>().Where(up => up.UserId == holder.Id).Select(up => up.PermissionName).ToList()
            .Should().BeEquivalentTo("Audit.View");
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    /// <summary>The host is shared, so put the sample plugin back exactly as the other tests expect it.</summary>
    private async Task RestoreAsync(ApiClient admin)
    {
        await admin.PostAsync("/api/plugins/cancel-pending", new { pluginId = Id });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Set<MajdsApp.Modules.Plugins.InstalledPlugin>().AnyAsync(p => p.Id == Id))
        {
            db.Set<MajdsApp.Modules.Plugins.InstalledPlugin>().Add(new MajdsApp.Modules.Plugins.InstalledPlugin
            {
                Id = Id, Name = "Tasks", Version = "1.0.1", Author = "Demo", AssemblyName = Id, IsEnabled = true,
                DiscoveredAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        scope.ServiceProvider.GetRequiredService<MajdsApp.Modules.Plugins.PluginStateCache>().Set(Id, true);
    }

    [Fact]
    public async Task Uninstalling_something_that_is_not_installed_is_a_404()
    {
        var admin = await factory.SignInAsync("un.admin2@example.com", "Admin");

        (await admin.PostAsync("/api/plugins/uninstall", new { pluginId = "Not.Installed" })).Status.Should().Be(HttpStatusCode.NotFound);
    }
}
