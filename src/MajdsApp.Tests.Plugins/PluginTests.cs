using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

public record TaskRow(Guid Id, string Title, string? Description, string Status);

/// <summary>P5 end to end: a real plugin assembly, built separately and never referenced by the host, is dropped
/// into the plugins folder and loaded at runtime into its own load context.</summary>
public class PluginTests : IClassFixture<PluginTests.PluginFactory>
{
    public class PluginFactory : ApiFactory
    {
        public PluginFactory() : base(null, CopyTasksPlugin) { }

        private static void CopyTasksPlugin(string pluginsFolder)
        {
            var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var configuration = baseDir.Parent!.Name; // Debug / Release
            var built = Path.GetFullPath(Path.Combine(baseDir.FullName, "..", "..", "..", "..",
                "MajdsApp.Plugins.Tasks", "bin", configuration, baseDir.Name));

            var target = Path.Combine(pluginsFolder, "MajdsApp.Plugins.Tasks");
            Directory.CreateDirectory(Path.Combine(target, "backend"));
            File.Copy(Path.Combine(built, "plugin.json"), Path.Combine(target, "plugin.json"));
            foreach (var file in Directory.GetFiles(built, "MajdsApp.Plugins.Tasks.*"))
                File.Copy(file, Path.Combine(target, "backend", Path.GetFileName(file)));
        }
    }

    private readonly PluginFactory _factory;
    public PluginTests(PluginFactory factory) => _factory = factory;

    private Task<ApiClient> AdminAsync() => _factory.SignInAsync("admin@example.com", "Admin");

    private record PluginRow(string Id, string Name, string Version, bool IsEnabled, string? LastError);
    private record MenuRow(string PluginId, string Label, string Route, string? Permission);
    private record PermissionGroupRow(string Name, List<string> Permissions);

    [Fact]
    public async Task The_plugin_is_discovered_registered_and_enabled_without_the_host_referencing_it()
    {
        var admin = await AdminAsync();

        var plugins = (await admin.GetAsync<List<PluginRow>>("/api/plugins/list")).Data!;

        plugins.Should().ContainSingle(p => p.Id == "MajdsApp.Plugins.Tasks")
            .Which.Should().Match<PluginRow>(p => p.IsEnabled && p.LastError == null && p.Version == "1.0.0");
    }

    [Fact]
    public async Task Its_permissions_and_menu_entry_appear_with_no_plugin_specific_host_code()
    {
        var admin = await AdminAsync();

        var tree = (await admin.GetAsync<List<PermissionGroupRow>>("/api/permissions/tree")).Data!;
        var menu = (await admin.GetAsync<List<MenuRow>>("/api/plugins/manifest")).Data!;

        tree.Should().Contain(g => g.Name == "Tasks").Which.Permissions
            .Should().BeEquivalentTo("Tasks.View", "Tasks.Create", "Tasks.Edit", "Tasks.Delete");
        menu.Should().ContainSingle(m => m.Route == "tasks" && m.Permission == "Tasks.View");
    }

    [Fact]
    public async Task The_plugin_api_enforces_its_permissions_like_any_core_module()
    {
        var plain = await _factory.SignInAsync("plain.tasks@example.com");

        var response = await plain.GetAsync<PagedData<TaskRow>>("/api/tasks/list?page=1&pageSize=5");

        response.Status.Should().Be(HttpStatusCode.Forbidden);
        response.Body!.Message.Should().Contain("Tasks.View");
    }

    [Fact]
    public async Task Full_create_read_update_delete_works_and_validation_applies()
    {
        var admin = await AdminAsync();

        var invalid = await admin.PostAsync<Guid>("/api/tasks/create", new { title = "", description = "x", status = "Todo", dueDate = (string?)null });
        var created = await admin.PostAsync<Guid>("/api/tasks/create", new { title = "Write tests", description = "all of them", status = "Todo", dueDate = (string?)null });
        var id = created.Data;
        await admin.PostAsync("/api/tasks/update", new { id, title = "Write tests", description = "all of them", status = "Done", dueDate = (string?)null });
        var read = await admin.GetAsync<TaskRow>($"/api/tasks/get?id={id}");
        await admin.PostAsync("/api/tasks/delete", new { id });
        var afterDelete = await admin.GetAsync<TaskRow>($"/api/tasks/get?id={id}");

        invalid.Status.Should().Be(HttpStatusCode.BadRequest);
        created.Status.Should().Be(HttpStatusCode.OK);
        read.Data!.Status.Should().Be("Done");
        afterDelete.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Disabling_the_plugin_blocks_its_api_immediately_and_enabling_restores_it()
    {
        var admin = await AdminAsync();

        await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = "MajdsApp.Plugins.Tasks", enabled = false });
        var blocked = await admin.GetAsync<PagedData<TaskRow>>("/api/tasks/list?page=1&pageSize=5");
        var menuWhileOff = (await admin.GetAsync<List<MenuRow>>("/api/plugins/manifest")).Data!;

        await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = "MajdsApp.Plugins.Tasks", enabled = true });
        var restored = await admin.GetAsync<PagedData<TaskRow>>("/api/tasks/list?page=1&pageSize=5");

        blocked.Status.Should().Be(HttpStatusCode.Forbidden);
        blocked.Body!.Message.Should().Contain("disabled");
        menuWhileOff.Should().BeEmpty();
        restored.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_holders_of_the_manage_permission_can_toggle_a_plugin()
    {
        var plain = await _factory.SignInAsync("nomanage@example.com");

        (await plain.PostAsync("/api/plugins/set-enabled", new { pluginId = "MajdsApp.Plugins.Tasks", enabled = false }))
            .Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.GetAsync<List<PluginRow>>("/api/plugins/list")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Toggling_an_unknown_plugin_is_a_404()
    {
        var admin = await AdminAsync();

        (await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = "Does.Not.Exist", enabled = true }))
            .Status.Should().Be(HttpStatusCode.NotFound);
    }
}

