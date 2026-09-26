using System.Text.Json.Nodes;
using FluentAssertions;
using MajdsApp.SharedKernel.Plugins;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-014/015/019: the manifest describes a pre-built UI, the menu tells the shell which element and bundle to load, and a bad declaration is refused.</summary>
[Collection(PluginHostCollection.Name)]
public class PluginFrontendTests(PluginHostFactory factory)
{
    private record MenuRow(string PluginId, string Route, string? Element, string? EntryUrl, string? StylesUrl, int? Contract, int? Angular);

    [Fact]
    public async Task The_menu_tells_the_shell_which_element_and_bundle_render_an_entry_and_leaves_the_others_to_the_metadata_page()
    {
        var admin = await factory.SignInAsync("pf.admin@example.com", "Admin");

        var menu = (await admin.GetAsync<List<MenuRow>>("/api/plugins/manifest")).Data!;

        menu.Single(m => m.Route == "tasks").Should().Match<MenuRow>(m => m.Element == null && m.EntryUrl == null);
        menu.Single(m => m.Route == "tasks-overview").Should().BeEquivalentTo(new MenuRow(
            "MajdsApp.Plugins.Tasks", "tasks-overview", "majds-tasks-overview",
            "/plugins/MajdsApp.Plugins.Tasks/main.js", "/plugins/MajdsApp.Plugins.Tasks/styles.css", 1, null));
    }

    private static string? LoadWith(Action<JsonObject> edit, Action<string>? files = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-plugin-fe-" + Guid.NewGuid().ToString("N"));
        try
        {
            PluginPackages.InstallFolder(folder);
            var path = Path.Combine(folder, "MajdsApp.Plugins.Tasks", "plugin.json");
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            edit(json);
            File.WriteAllText(path, json.ToJsonString());
            files?.Invoke(Path.Combine(folder, "MajdsApp.Plugins.Tasks"));
            return PluginManager.LoadAll(folder).Single().LoadError;
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void A_valid_declaration_loads()
    {
        LoadWith(_ => { }).Should().BeNull();
    }

    [Fact]
    public void An_element_with_no_frontend_declared_is_refused()
    {
        LoadWith(json => json.Remove("frontend")).Should().Contain("no frontend entry");
    }

    [Fact]
    public void A_frontend_file_that_is_not_in_the_package_is_refused()
    {
        LoadWith(json => json["frontend"]!["entry"] = "gone.js").Should().Contain("gone.js");
        LoadWith(json => json["frontend"]!["styles"] = "gone.css").Should().Contain("gone.css");
    }

    [Fact]
    public void A_frontend_without_a_contract_version_is_refused()
    {
        LoadWith(json => json["frontend"]!["contract"] = 0).Should().Contain("contract");
    }

    [Fact]
    public void An_entry_that_climbs_out_of_the_frontend_folder_is_refused()
    {
        LoadWith(json => json["frontend"]!["entry"] = "../plugin.json").Should().Contain("not found");
    }
}
