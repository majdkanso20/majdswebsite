using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Caching;

/// <summary>Keeps values in this process (single node). Nothing is serialized.</summary>
public sealed class MemoryCacheService(IMemoryCache cache, CacheOptions options) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
        Task.FromResult(cache.TryGetValue(options.KeyFor(key), out T? value) ? value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken ct = default)
    {
        cache.Set(options.KeyFor(key), value, timeToLive);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken ct = default)
    {
        if (cache.TryGetValue(options.KeyFor(key), out T? existing) && existing is not null) return existing;

        var created = await factory(ct);
        cache.Set(options.KeyFor(key), created, timeToLive);
        return created;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Remove(string key) => cache.Remove(options.KeyFor(key));
}

/// <summary>
/// Stores values as JSON in a shared <see cref="IDistributedCache"/> (Redis, or any other), so every node sees the same values and a removal on
/// one node takes effect on all (AC-CACHE-1). A cache that cannot be reached is a miss and is logged, so a cache outage slows the platform down
/// (it reads from the database again) instead of taking it down.
/// </summary>
public sealed class DistributedCacheService(IDistributedCache cache, CacheOptions options, ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cache.GetAsync(options.KeyFor(key), ct);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}; treating it as a miss", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken ct = default)
    {
        try
        {
            await cache.SetAsync(options.KeyFor(key), JsonSerializer.SerializeToUtf8Bytes(value, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken ct = default)
    {
        var existing = await GetAsync<T>(key, ct);
        if (existing is not null) return existing;

        var created = await factory(ct);
        await SetAsync(key, created, timeToLive, ct);
        return created;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(options.KeyFor(key), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache removal failed for {Key}; the value expires on its own", key);
        }
    }

    public void Remove(string key)
    {
        try
        {
            cache.Remove(options.KeyFor(key));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache removal failed for {Key}; the value expires on its own", key);
        }
    }
}
