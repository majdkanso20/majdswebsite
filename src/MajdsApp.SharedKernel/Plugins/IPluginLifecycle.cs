using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>What a plugin's hook is told about the moment it runs; <see cref="Services"/> is a scope that ends when the hook returns.</summary>
public record PluginHookContext(string PluginId, string Version, IServiceProvider Services);

/// <summary>
/// Optional: a plugin's module (the type named by <c>moduleType</c>) may also implement this to seed, migrate or clean up its data
/// (P5 FR-PLUG-031). Every hook has an empty default, so implement only the ones needed, and make each one safe to run twice.
/// </summary>
public interface IPluginLifecycle
{
    /// <summary>The first start after the plugin was installed.</summary>
    Task OnInstallAsync(PluginHookContext context, CancellationToken ct) => Task.CompletedTask;

    /// <summary>The first start after the plugin's version changed (an upgrade or a rollback); <paramref name="fromVersion"/> is the version it replaced.</summary>
    Task OnUpgradeAsync(PluginHookContext context, string fromVersion, CancellationToken ct) => Task.CompletedTask;

    /// <summary>An administrator enabled the plugin. If this throws, the plugin stays disabled and the error is shown.</summary>
    Task OnEnableAsync(PluginHookContext context, CancellationToken ct) => Task.CompletedTask;

    /// <summary>An administrator disabled the plugin. Its data is kept.</summary>
    Task OnDisableAsync(PluginHookContext context, CancellationToken ct) => Task.CompletedTask;

    /// <summary>An administrator uninstalled the plugin. It runs after the plugin's grants and registry entry are gone.</summary>
    Task OnUninstallAsync(PluginHookContext context, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Runs a plugin's hook and contains its failure: a broken plugin reports an error, it never takes the host down (P5 FR-PLUG-031/037).</summary>
public static class PluginHooks
{
    /// <summary>Null when the hook ran (or the plugin has none); otherwise the failure message.</summary>
    public static async Task<string?> RunAsync(
        LoadedPlugin plugin, IServiceProvider services, Func<IPluginLifecycle, PluginHookContext, Task> hook, string name, ILogger? logger = null)
    {
        if (plugin.Module is not IPluginLifecycle lifecycle) return null;

        try
        {
            using var scope = services.CreateScope();
            await hook(lifecycle, new PluginHookContext(plugin.Manifest.Id, plugin.Manifest.Version, scope.ServiceProvider));
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Plugin '{Plugin}' failed in {Hook}", plugin.Manifest.Id, name);
            return $"{name} failed: {ex.Message}";
        }
    }
}
