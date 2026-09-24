using MajdsApp.Data;
using MajdsApp.SharedKernel.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MajdsApp.Modules.Features;

/// <summary>Database-backed feature checker: override if present, else the definition's default.
/// Cached like <c>SettingsProvider</c> and invalidated on change.</summary>
public class FeatureChecker(ApplicationDbContext db, IMemoryCache cache) : IFeatureChecker
{
    private const string CacheKey = "features:effective";

    public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default) =>
        (await LoadAsync(ct)).TryGetValue(name, out var enabled) && enabled;

    public async Task<IReadOnlyCollection<string>> GetEnabledAsync(CancellationToken ct = default) =>
        (await LoadAsync(ct)).Where(kv => kv.Value).Select(kv => kv.Key).ToList();

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var overrides = await db.Set<FeatureOverride>().AsNoTracking().ToDictionaryAsync(f => f.Name, f => f.IsEnabled, ct);
            return (IReadOnlyDictionary<string, bool>)FeatureDefinitionRegistry.GetAll()
                .ToDictionary(f => f.Name, f => overrides.TryGetValue(f.Name, out var v) ? v : f.DefaultEnabled);
        }) ?? new Dictionary<string, bool>();
}
