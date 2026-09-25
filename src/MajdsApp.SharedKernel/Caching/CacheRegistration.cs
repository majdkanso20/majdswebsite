using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.SharedKernel.Caching;

/// <summary>Which cache backs <see cref="ICacheService"/> and how keys are named (F-Caching FR-CACHE-002).</summary>
public sealed record CacheOptions(string Provider, string KeyPrefix)
{
    public const string Memory = "Memory";
    public const string Redis = "Redis";
    public const string Distributed = "Distributed";

    /// <summary>The key as stored: prefixed, so several applications can share one Redis without touching each other's entries.</summary>
    public string KeyFor(string key) => KeyPrefix + key;
}

public static class CacheRegistration
{
    /// <summary>
    /// <c>Cache:Provider</c> selects the backend, all behind <see cref="ICacheService"/>:
    /// <c>Memory</c> (default; one node), <c>Redis</c> (<c>Cache:Redis:ConnectionString</c>; shared by every node) or <c>Distributed</c>
    /// (a distributed cache held in this process, which exercises the serializing path without a server).
    /// </summary>
    public static IServiceCollection AddPlatformCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Cache:Provider"] is { Length: > 0 } configured ? configured : CacheOptions.Memory;
        var prefix = configuration["Cache:KeyPrefix"] is { Length: > 0 } p ? p : "majds:";
        var options = new CacheOptions(provider, prefix);
        services.AddSingleton(options);
        services.AddMemoryCache();

        if (provider.Equals(CacheOptions.Redis, StringComparison.OrdinalIgnoreCase))
        {
            var connection = configuration["Cache:Redis:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Cache:Provider is Redis, but Cache:Redis:ConnectionString is not set.");

            services.AddStackExchangeRedisCache(o => o.Configuration = connection);
            services.AddSingleton<ICacheService, DistributedCacheService>();
        }
        else if (provider.Equals(CacheOptions.Distributed, StringComparison.OrdinalIgnoreCase))
        {
            services.AddDistributedMemoryCache();
            services.AddSingleton<ICacheService, DistributedCacheService>();
        }
        else if (provider.Equals(CacheOptions.Memory, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }
        else
        {
            throw new InvalidOperationException($"Cache:Provider '{provider}' is not supported. Use Memory, Redis or Distributed.");
        }

        return services;
    }
}
