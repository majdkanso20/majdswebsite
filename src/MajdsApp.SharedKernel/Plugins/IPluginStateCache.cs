namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// Whether a given plugin assembly is currently enabled (P5 FR-PLUG-033: disabling a plugin removes
/// its API availability immediately, without unloading it from memory). Default is "always enabled" —
/// same no-op-by-default pattern as <c>ISettingsProvider</c>/<c>IFeatureChecker</c>; the Plugins
/// management module overrides it with a DB-backed, cache-invalidated implementation.
/// </summary>
public interface IPluginStateCache
{
    bool IsEnabled(string pluginAssemblyName);
}

public class AllowAllPluginStateCache : IPluginStateCache
{
    public bool IsEnabled(string pluginAssemblyName) => true;
}
