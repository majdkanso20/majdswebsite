using MajdsApp.Data;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.EntityFrameworkCore;

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
        ApplicationDbContext db, IReadOnlyList<LoadedPlugin> loaded, PluginStateCache cache, CancellationToken ct = default)
    {
        foreach (var plugin in loaded)
        {
            var existing = await db.Set<InstalledPlugin>().FirstOrDefaultAsync(p => p.Id == plugin.Manifest.Id, ct);
            var assemblyName = plugin.Assembly?.GetName().Name ?? plugin.Manifest.Id;

            if (existing is null)
            {
                existing = new InstalledPlugin { Id = plugin.Manifest.Id, IsEnabled = true, DiscoveredAt = DateTime.UtcNow };
                db.Set<InstalledPlugin>().Add(existing);
            }

            existing.Name = plugin.Manifest.Name;
            existing.Version = plugin.Manifest.Version;
            existing.Author = plugin.Manifest.Author;
            existing.AssemblyName = assemblyName;
            existing.LastError = plugin.LoadError;
            existing.LastSeenAt = DateTime.UtcNow;

            cache.Set(assemblyName, existing.IsEnabled);
        }

        await db.SaveChangesAsync(ct);
    }
}
