using FluentAssertions;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>P4 FR-XC-003: a query that opts in with ICacheableQuery is answered from the cache, and dropped when what it describes changes.</summary>
public class CacheableQueryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CacheKey = "query:features.enabled";

    [Fact]
    public async Task The_enabled_features_are_cached_after_the_first_request_and_refreshed_the_moment_one_is_switched()
    {
        var admin = await factory.SignInAsync("cq.admin@example.com", "Admin");
        var cache = factory.Services.GetRequiredService<ICacheService>();
        cache.Remove(CacheKey);

        var first = (await admin.GetAsync<List<string>>("/api/features/enabled")).Data!;
        var cached = await cache.GetAsync<List<string>>(CacheKey);
        cached.Should().NotBeNull("the answer was stored for the next request");
        cached!.Should().BeEquivalentTo(first);

        var feature = first.First(f => f != "Files");                      // any feature that can be switched off and on
        await admin.PostAsync("/api/features/set", new { name = feature, enabled = false });
        try
        {
            (await cache.GetAsync<List<string>>(CacheKey)).Should().BeNull("switching a feature drops the cached answer");
            (await admin.GetAsync<List<string>>("/api/features/enabled")).Data!.Should().NotContain(feature);
        }
        finally
        {
            await admin.PostAsync("/api/features/set", new { name = feature, enabled = true });
        }

        (await admin.GetAsync<List<string>>("/api/features/enabled")).Data!.Should().Contain(feature);
    }
}
