using System.Collections.Concurrent;
using MajdsApp.SharedKernel.Plugins;

namespace MajdsApp.Modules.Plugins;

/// <summary>
/// DB-backed replacement for the SharedKernel's default "always enabled" <see cref="IPluginStateCache"/>
/// (same override-the-default idiom as every other cross-cutting default in this platform). Held
/// in-memory as a singleton so the per-request <see cref="MajdsApp.SharedKernel.Plugins.PluginGateFilter"/>
/// never hits the database; <see cref="Set"/> is called the moment an admin toggles a plugin, and at
/// startup once the registry has been synced from the database (see Program.cs).
/// </summary>
public class PluginStateCache : IPluginStateCache
{
    private readonly ConcurrentDictionary<string, bool> _enabled = new();

    public bool IsEnabled(string pluginAssemblyName) => _enabled.GetValueOrDefault(pluginAssemblyName, true);

    public void Set(string pluginAssemblyName, bool enabled) => _enabled[pluginAssemblyName] = enabled;
}
