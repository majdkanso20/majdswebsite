using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-030/040/041 (state, diagnostics, settings entry point) and FR-PLUG-029 (a plugin cannot grant itself anything).</summary>
[Collection(PluginHostCollection.Name)]
public class PluginDiagnosticsTests(PluginHostFactory factory)
{
    private record Diagnostic(string Check, bool Ok, string Detail);
    private record Row(string Id, string State, List<Diagnostic> Diagnostics, string? SettingsGroup, bool IsEnabled);

    [Fact]
    public async Task The_list_shows_each_plugins_state_its_checks_and_where_its_settings_are()
    {
        var admin = await factory.SignInAsync("pd.admin1@example.com", "Admin");

        var tasks = (await admin.GetAsync<List<Row>>("/api/plugins/list")).Data!.Single(p => p.Id == "MajdsApp.Plugins.Tasks");

        tasks.State.Should().Be("Enabled");
        tasks.Diagnostics.Should().Contain(d => d.Check == "Platform version" && d.Ok);
        tasks.Diagnostics.Should().Contain(d => d.Check == "UI contract" && d.Ok);
        tasks.Diagnostics.Should().NotContain(d => !d.Ok);
        tasks.SettingsGroup.Should().Be("Tasks");
    }

    [Fact]
    public async Task A_disabled_plugin_is_shown_as_disabled_and_enabled_again()
    {
        var admin = await factory.SignInAsync("pd.admin2@example.com", "Admin");
        try
        {
            await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = "MajdsApp.Plugins.Tasks", enabled = false });
            (await admin.GetAsync<List<Row>>("/api/plugins/list")).Data!.Single(p => p.Id == "MajdsApp.Plugins.Tasks").State.Should().Be("Disabled");
        }
        finally
        {
            await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = "MajdsApp.Plugins.Tasks", enabled = true });
        }

        (await admin.GetAsync<List<Row>>("/api/plugins/list")).Data!.Single(p => p.Id == "MajdsApp.Plugins.Tasks").State.Should().Be("Enabled");
    }

    [Fact]
    public async Task Installing_a_plugin_grants_its_permissions_to_no_one_except_the_administrator_role()
    {
        var plain = await factory.SignInAsync("pd.plain@example.com");
        var admin = await factory.SignInAsync("pd.admin3@example.com", "Admin");

        var plainPermissions = (await plain.GetAsync<List<string>>("/api/session/permissions")).Data!;
        var adminPermissions = (await admin.GetAsync<List<string>>("/api/session/permissions")).Data!;

        plainPermissions.Should().NotContain(p => p.StartsWith("Tasks."));
        adminPermissions.Should().Contain("Tasks.View");
    }
}
