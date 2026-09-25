namespace MajdsApp.SharedKernel.Caching;

/// <summary>
/// The platform's cache (F-Caching FR-CACHE-001): get, set with a time to live, remove, and get-or-add. Code depends on this, never on
/// a concrete cache, so the backend is a configuration choice (<c>Cache:Provider</c>): in memory for one node, or a distributed cache
/// such as Redis shared by every node (FR-CACHE-002, AC-CACHE-1). Values must be serializable to JSON, since a distributed backend stores them
/// outside the process; the in-memory backend keeps the object itself.
/// </summary>
public interface ICacheService
{
    /// <summary>The cached value, or <c>default</c> when there is none, it has expired, or the cache cannot be reached (a cache problem is a miss, never an error).</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken ct = default);

    /// <summary>Returns the cached value, or runs <paramref name="factory"/>, caches what it returns for <paramref name="timeToLive"/>, and returns that.</summary>
    Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Synchronous removal for the invalidation hooks that are not asynchronous (for example after a settings write).</summary>
    void Remove(string key);
}
