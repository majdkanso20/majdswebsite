using MajdsApp.SharedKernel.Caching;
using MediatR;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Caches the result of any request implementing <see cref="ICacheableQuery"/> (FR-XC-002/003) through <see cref="ICacheService"/>, so the same
/// requests are cached in memory on one node or in a shared distributed cache across nodes, by configuration alone (F-Caching FR-CACHE-002).
/// A cached response must be serializable to JSON.
/// </summary>
public class CachingBehavior<TRequest, TResponse>(ICacheService cache) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery cacheable)
            return await next(ct);

        return await cache.GetOrAddAsync($"query:{cacheable.CacheKey}", _ => next(ct), TimeSpan.FromSeconds(cacheable.AbsoluteExpirationSeconds), ct);
    }
}
