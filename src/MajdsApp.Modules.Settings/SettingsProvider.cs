using MajdsApp.Data;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.SharedKernel.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Settings;

/// <summary>
/// Real F-Settings implementation, replacing SharedKernel's defaults-only default. Effective value =
/// the admin override in <see cref="SettingValue"/> if one exists, else the definition's default —
/// resolved once per cache window, same shape as F-Authorization's <c>PermissionChecker</c>.
/// </summary>
public class SettingsProvider(ApplicationDbContext db, ICacheService cache, IDataProtectionProvider dataProtection) : ISettingsProvider
{
    private const string CacheKey = "settings:effective";

    public async Task<string> GetAsync(string name, CancellationToken ct = default)
    {
        var effective = await GetAllAsync(ct);
        if (effective.TryGetValue(name, out var value))
            return value;

        return SettingDefinitionRegistry.Find(name)?.DefaultValue ?? string.Empty;
    }

    public async Task<bool> GetBooleanAsync(string name, CancellationToken ct = default) =>
        bool.TryParse(await GetAsync(name, ct), out var value) && value;

    public async Task<int> GetIntegerAsync(string name, CancellationToken ct = default) =>
        int.TryParse(await GetAsync(name, ct), out var value) ? value : 0;

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        return await cache.GetOrAddAsync(CacheKey, async token =>
        {
            var overrides = await db.Set<SettingValue>().ToDictionaryAsync(s => s.Id, s => s.Value, token);
            var result = new Dictionary<string, string>();
            foreach (var definition in SettingDefinitionRegistry.GetAll())
            {
                if (!overrides.TryGetValue(definition.Name, out var value))
                    result[definition.Name] = definition.DefaultValue;
                else
                    result[definition.Name] = definition.IsSensitive ? SettingSecrets.Unprotect(dataProtection, value) : value;
            }

            return result;
        }, TimeSpan.FromMinutes(5), ct);
    }

    public void Invalidate() => cache.Remove(CacheKey);

    public async Task<IReadOnlyDictionary<string, string>> GetAllForUserAsync(string? userId, CancellationToken ct = default)
    {
        var app = await GetAllAsync(ct);
        if (string.IsNullOrEmpty(userId))
            return app;

        var overrides = await cache.GetOrAddAsync(UserCacheKey(userId),
            token => db.Set<SettingUserValue>().Where(s => s.UserId == userId).ToDictionaryAsync(s => s.Name, s => s.Value, token),
            TimeSpan.FromMinutes(5), ct);

        var allowed = SettingDefinitionRegistry.GetAll().Where(d => d.AllowUserOverride).Select(d => d.Name).ToHashSet();
        var result = new Dictionary<string, string>(app);
        foreach (var (name, value) in overrides)
            if (allowed.Contains(name))
                result[name] = value;

        return result;
    }

    public void InvalidateUser(string userId) => cache.Remove(UserCacheKey(userId));

    private static string UserCacheKey(string userId) => $"settings:user:{userId}";
}
