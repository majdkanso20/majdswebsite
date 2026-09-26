using MajdsApp.Data;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Plugins;

/// <summary>
/// Runs once at startup (see Program.cs, after <c>PluginManager.LoadAll</c> and after the DB is ready)
/// to reconcile the discovered plugins with the persisted registry (P5 FR-PLUG-030): a newly-seen
/// plugin is inserted as enabled; a previously-known plugin keeps whatever enabled/disabled state an
/// admin last set, even though it was just re-discovered fresh from disk. Also seeds the in-memory
/// <see cref="PluginStateCache"/> so the very first request after a restart already reflects the
/// persisted state, not just future toggles.
/// </summary>
public static class PluginRegistrySync
{
    public static async Task SyncAsync(
        ApplicationDbContext db, IReadOnlyList<LoadedPlugin> loaded, PluginStateCache cache,
        IServiceProvider? services = null, ILogger? logger = null, CancellationToken ct = default)
    {
        foreach (var plugin in loaded)
        {
            var existing = await db.Set<InstalledPlugin>().FirstOrDefaultAsync(p => p.Id == plugin.Manifest.Id, ct);
            var assemblyName = plugin.Assembly?.GetName().Name ?? plugin.Manifest.Id;

            // FR-PLUG-031: the plugin's install and upgrade hooks run the first time the host sees it, or sees a different version of it.
            string? hook = null;
            string? hookName = null;
            if (existing is null)
            {
                existing = new InstalledPlugin { Id = plugin.Manifest.Id, IsEnabled = true, DiscoveredAt = DateTime.UtcNow };
                db.Set<InstalledPlugin>().Add(existing);
                hookName = "OnInstall";
                hook = "install";
            }
            else if (existing.Version != plugin.Manifest.Version && plugin.Succeeded)
            {
                hookName = "OnUpgrade";
                hook = existing.Version;
            }
            var fromVersion = existing.Version;

            existing.Name = plugin.Manifest.Name;
            existing.Version = plugin.Manifest.Version;
            existing.Author = plugin.Manifest.Author;
            existing.AssemblyName = assemblyName;
            existing.LastError = plugin.LoadError;
            if (hookName is not null && plugin.Succeeded && services is not null)
            {
                existing.LastError = hook == "install"
                    ? await PluginHooks.RunAsync(plugin, services, (h, c) => h.OnInstallAsync(c, ct), hookName, logger)
                    : await PluginHooks.RunAsync(plugin, services, (h, c) => h.OnUpgradeAsync(c, fromVersion, ct), hookName, logger);
            }
            existing.LastSeenAt = DateTime.UtcNow;

            cache.Set(assemblyName, existing.IsEnabled);
        }

        await db.SaveChangesAsync(ct);
    }
}
