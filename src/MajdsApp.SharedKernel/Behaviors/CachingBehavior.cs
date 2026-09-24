using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Caches the result of any request implementing <see cref="ICacheableQuery"/> (FR-XC-002/003).
/// Backed by <see cref="IMemoryCache"/> for now; F-Caching later swaps this to a distributed
/// (Redis) backend behind the same contract without touching callers (NFR-SCALE-2).
/// </summary>
public class CachingBehavior<TRequest, TResponse>(IMemoryCache cache) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery cacheable)
            return await next(ct);

        if (cache.TryGetValue(cacheable.CacheKey, out TResponse? cached) && cached is not null)
            return cached;

        var response = await next(ct);
        cache.Set(cacheable.CacheKey, response, TimeSpan.FromSeconds(cacheable.AbsoluteExpirationSeconds));
        return response;
    }
}
