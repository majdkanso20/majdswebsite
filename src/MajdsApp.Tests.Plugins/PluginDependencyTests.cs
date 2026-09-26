using System.Text.Json.Nodes;
using FluentAssertions;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-010 (dependencies and load order) and FR-PLUG-031 (lifecycle hooks).</summary>
[Collection(PluginHostCollection.Name)] // serialized with the host tests: one test briefly loads the sample plugin assembly
public class PluginDependencyTests
{
    private static LoadedPlugin Plugin(string id, string version = "1.0.0", params PluginDependency[] dependsOn) =>
        new(new PluginManifest(id, id, version, "t", "T", [], Dependencies: dependsOn), null, null, null);

    private static string? Problem(IReadOnlyList<LoadedPlugin> result, string id) => result.Single(p => p.Manifest.Id == id).LoadError;

    [Fact]
    public void A_plugin_loads_after_the_plugins_it_depends_on()
    {
        var result = PluginDependencyResolver.Resolve([
            Plugin("c", dependsOn: [new PluginDependency("b")]),
            Plugin("b", dependsOn: [new PluginDependency("a")]),
            Plugin("a")]);

        result.Select(p => p.Manifest.Id).Should().Equal("a", "b", "c");
        result.Should().OnlyContain(p => p.Succeeded);
    }

    [Fact]
    public void A_plugin_with_a_missing_dependency_is_left_disabled_with_a_reason_and_the_rest_still_load()
    {
        var result = PluginDependencyResolver.Resolve([Plugin("needy", dependsOn: [new PluginDependency("ghost")]), Plugin("fine")]);

        Problem(result, "needy").Should().Contain("ghost").And.Contain("not installed");
        Problem(result, "fine").Should().BeNull();
    }

    [Fact]
    public void A_dependency_outside_the_version_range_is_refused()
    {
        var result = PluginDependencyResolver.Resolve([
            Plugin("base", "1.2.0"),
            Plugin("too-new", dependsOn: [new PluginDependency("base", MinVersion: "2.0.0")]),
            Plugin("too-old", dependsOn: [new PluginDependency("base", MaxVersion: "1.1.0")]),
            Plugin("just-right", dependsOn: [new PluginDependency("base", "1.0.0", "1.5.0")])]);

        Problem(result, "too-new").Should().Contain("2.0.0 or newer").And.Contain("1.2.0");
        Problem(result, "too-old").Should().Contain("up to 1.1.0");
        Problem(result, "just-right").Should().BeNull();
    }

    [Fact]
    public void A_plugin_that_needs_a_plugin_which_failed_is_refused_too()
    {
        var broken = new LoadedPlugin(new PluginManifest("broken", "broken", "1.0.0", "t", "T", []), null, null, "bad assembly");

        var result = PluginDependencyResolver.Resolve([broken, Plugin("child", dependsOn: [new PluginDependency("broken")]), Plugin("grandchild", dependsOn: [new PluginDependency("child")])]);

        Problem(result, "child").Should().Contain("could not be loaded");
        Problem(result, "grandchild").Should().Contain("child").And.Contain("cannot be used");
    }

    [Fact]
    public void A_dependency_cycle_disables_every_plugin_in_it_and_names_the_cycle()
    {
        var result = PluginDependencyResolver.Resolve([
            Plugin("x", dependsOn: [new PluginDependency("y")]),
            Plugin("y", dependsOn: [new PluginDependency("x")]),
            Plugin("free")]);

        Problem(result, "x").Should().Contain("cycle");
        Problem(result, "y").Should().Contain("cycle");
        Problem(result, "free").Should().BeNull();
    }

    [Fact]
    public void The_manifest_on_disk_can_declare_dependencies_and_a_plugin_that_needs_a_missing_one_is_not_loaded()
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-plugin-deps-" + Guid.NewGuid().ToString("N"));
        try
        {
            PluginPackages.InstallFolder(folder);
            var path = Path.Combine(folder, "MajdsApp.Plugins.Tasks", "plugin.json");
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            json["dependencies"] = new JsonArray(new JsonObject { ["id"] = "Acme.Missing", ["minVersion"] = "1.0.0" });
            File.WriteAllText(path, json.ToJsonString());

            var loaded = PluginManager.LoadAll(folder).Single();

            loaded.Succeeded.Should().BeFalse();
            loaded.Assembly.Should().BeNull("a plugin that cannot be satisfied is not partly loaded");
            loaded.LoadError.Should().Contain("Acme.Missing");
            loaded.Manifest.DependsOn.Should().ContainSingle(d => d.Id == "Acme.Missing" && d.MinVersion == "1.0.0");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    // ---- lifecycle hooks ---------------------------------------------------------------------------------------------------------

    private class Recorder
    {
        public List<string> Calls { get; } = [];
    }

    private class HookedModule(bool failEnable = false) : IFeatureModule, IPluginLifecycle
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public Task OnEnableAsync(PluginHookContext context, CancellationToken ct)
        {
            context.Services.GetRequiredService<Recorder>().Calls.Add($"enable:{context.PluginId}:{context.Version}");
            return failEnable ? throw new InvalidOperationException("no disk") : Task.CompletedTask;
        }

        public Task OnUpgradeAsync(PluginHookContext context, string fromVersion, CancellationToken ct)
        {
            context.Services.GetRequiredService<Recorder>().Calls.Add($"upgrade-from:{fromVersion}");
            return Task.CompletedTask;
        }
    }

    private static (ServiceProvider Services, Recorder Recorder) Host()
    {
        var recorder = new Recorder();
        return (new ServiceCollection().AddSingleton(recorder).BuildServiceProvider(), recorder);
    }

    private static LoadedPlugin WithModule(IFeatureModule module) =>
        new(new PluginManifest("hooked", "Hooked", "2.0.0", "t", "T", []), null, module, null);

    [Fact]
    public async Task A_hook_runs_with_the_plugin_identity_and_a_service_scope()
    {
        var (services, recorder) = Host();

        var failure = await PluginHooks.RunAsync(WithModule(new HookedModule()), services, (h, c) => h.OnEnableAsync(c, default), "OnEnable");
        await PluginHooks.RunAsync(WithModule(new HookedModule()), services, (h, c) => h.OnUpgradeAsync(c, "1.0.0", default), "OnUpgrade");

        failure.Should().BeNull();
        recorder.Calls.Should().Equal("enable:hooked:2.0.0", "upgrade-from:1.0.0");
    }

    [Fact]
    public async Task A_hook_the_plugin_did_not_implement_does_nothing()
    {
        var (services, recorder) = Host();

        // HookedModule leaves OnDisable at its empty default, and a module with no lifecycle at all is simply skipped.
        (await PluginHooks.RunAsync(WithModule(new HookedModule()), services, (h, c) => h.OnDisableAsync(c, default), "OnDisable")).Should().BeNull();
        (await PluginHooks.RunAsync(WithModule(new NoLifecycle()), services, (h, c) => h.OnEnableAsync(c, default), "OnEnable")).Should().BeNull();
        recorder.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_hook_that_throws_is_contained_and_reported_never_raised()
    {
        var (services, _) = Host();

        var failure = await PluginHooks.RunAsync(WithModule(new HookedModule(failEnable: true)), services, (h, c) => h.OnEnableAsync(c, default), "OnEnable");

        failure.Should().Be("OnEnable failed: no disk");
    }

    private class NoLifecycle : IFeatureModule
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
    }
}
