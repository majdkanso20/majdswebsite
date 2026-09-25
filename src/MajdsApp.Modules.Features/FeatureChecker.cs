using MajdsApp.Data;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.SharedKernel.Features;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Features;

/// <summary>Database-backed feature checker: override if present, else the definition's default.
/// Cached like <c>SettingsProvider</c> and invalidated on change.</summary>
public class FeatureChecker(ApplicationDbContext db, ICacheService cache) : IFeatureChecker
{
    private const string CacheKey = "features:effective";

    public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default) =>
        (await LoadAsync(ct)).TryGetValue(name, out var enabled) && enabled;

    public async Task<IReadOnlyCollection<string>> GetEnabledAsync(CancellationToken ct = default) =>
        (await LoadAsync(ct)).Where(kv => kv.Value).Select(kv => kv.Key).ToList();

    public void Invalidate() => cache.Remove(CacheKey);

    private Task<Dictionary<string, bool>> LoadAsync(CancellationToken ct) =>
        cache.GetOrAddAsync(CacheKey, async token =>
        {
            var overrides = await db.Set<FeatureOverride>().AsNoTracking().ToDictionaryAsync(f => f.Name, f => f.IsEnabled, token);
            return FeatureDefinitionRegistry.GetAll()
                .ToDictionary(f => f.Name, f => overrides.TryGetValue(f.Name, out var v) ? v : f.DefaultEnabled);
        }, TimeSpan.FromMinutes(5), ct);
}
