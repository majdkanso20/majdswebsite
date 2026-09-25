using FluentAssertions;
using MajdsApp.SharedKernel.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class CacheServiceTests
{
    public record Sample(string Name, int Count, List<string> Tags, Dictionary<string, bool> Flags);

    private static readonly CacheOptions Options = new(CacheOptions.Memory, "test:");

    private static ICacheService Memory() => new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), Options);

    private static ICacheService Distributed(IDistributedCache? shared = null, string prefix = "test:") =>
        new DistributedCacheService(shared ?? new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())),
            new CacheOptions(CacheOptions.Distributed, prefix), NullLogger<DistributedCacheService>.Instance);

    public static TheoryData<string> Backends => new() { "memory", "distributed" };

    private static ICacheService Create(string backend) => backend == "memory" ? Memory() : Distributed();

    // ---- the same contract on every backend (FR-CACHE-001/002) --------------------------------------------------------

    [Theory, MemberData(nameof(Backends))]
    public async Task A_value_can_be_stored_read_and_removed(string backend)
    {
        var cache = Create(backend);

        (await cache.GetAsync<string>("k")).Should().BeNull();
        await cache.SetAsync("k", "value", TimeSpan.FromMinutes(1));
        (await cache.GetAsync<string>("k")).Should().Be("value");

        await cache.RemoveAsync("k");
        (await cache.GetAsync<string>("k")).Should().BeNull();
    }

    [Theory, MemberData(nameof(Backends))]
    public async Task Removing_synchronously_works_too(string backend)
    {
        var cache = Create(backend);
        await cache.SetAsync("k", 5, TimeSpan.FromMinutes(1));

        cache.Remove("k");

        (await cache.GetAsync<int?>("k")).Should().BeNull();
    }

    [Theory, MemberData(nameof(Backends))]
    public async Task A_value_expires_after_its_time_to_live(string backend)
    {
        var cache = Create(backend);
        await cache.SetAsync("k", "short-lived", TimeSpan.FromMilliseconds(150));

        (await cache.GetAsync<string>("k")).Should().Be("short-lived");
        await Task.Delay(400);
        (await cache.GetAsync<string>("k")).Should().BeNull();
    }

    [Theory, MemberData(nameof(Backends))]
    public async Task Get_or_add_runs_the_factory_once_and_serves_the_cached_value_after(string backend)
    {
        var cache = Create(backend);
        var runs = 0;

        Task<string> Factory(CancellationToken _) { runs++; return Task.FromResult("computed"); }

        (await cache.GetOrAddAsync("k", Factory, TimeSpan.FromMinutes(1))).Should().Be("computed");
        (await cache.GetOrAddAsync("k", Factory, TimeSpan.FromMinutes(1))).Should().Be("computed");
        runs.Should().Be(1);

        await cache.RemoveAsync("k");
        await cache.GetOrAddAsync("k", Factory, TimeSpan.FromMinutes(1));
        runs.Should().Be(2);                                                            // invalidated: the next read recomputes (AC-CACHE-2)
    }

    [Theory, MemberData(nameof(Backends))]
    public async Task Structured_values_come_back_intact(string backend)
    {
        var cache = Create(backend);
        var sample = new Sample("Ada", 3, ["a", "b"], new Dictionary<string, bool> { ["x"] = true, ["y"] = false });

        await cache.SetAsync("sample", sample, TimeSpan.FromMinutes(1));
        var read = await cache.GetAsync<Sample>("sample");

        read.Should().BeEquivalentTo(sample);
    }

    [Theory, MemberData(nameof(Backends))]
    public async Task Different_keys_do_not_interfere(string backend)
    {
        var cache = Create(backend);
        await cache.SetAsync("a", "1", TimeSpan.FromMinutes(1));
        await cache.SetAsync("b", "2", TimeSpan.FromMinutes(1));

        await cache.RemoveAsync("a");

        (await cache.GetAsync<string>("a")).Should().BeNull();
        (await cache.GetAsync<string>("b")).Should().Be("2");
    }

    // ---- shared between nodes (AC-CACHE-1) -------------------------------------------------------------------------------------

    [Fact]
    public async Task Two_nodes_sharing_a_distributed_cache_see_each_others_values_and_removals()
    {
        var shared = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var nodeA = Distributed(shared);
        var nodeB = Distributed(shared);

        await nodeA.SetAsync("settings:effective", new Dictionary<string, string> { ["General.ApplicationName"] = "Acme" }, TimeSpan.FromMinutes(1));
        (await nodeB.GetAsync<Dictionary<string, string>>("settings:effective"))!["General.ApplicationName"].Should().Be("Acme");

        nodeB.Remove("settings:effective");                                            // a write on node B ...
        (await nodeA.GetAsync<Dictionary<string, string>>("settings:effective")).Should().BeNull();   // ... is felt by node A
    }

    [Fact]
    public async Task Two_memory_caches_do_not_share_anything_which_is_why_several_nodes_need_a_distributed_cache()
    {
        var nodeA = Memory();
        var nodeB = Memory();

        await nodeA.SetAsync("k", "only on A", TimeSpan.FromMinutes(1));

        (await nodeB.GetAsync<string>("k")).Should().BeNull();
    }

    [Fact]
    public async Task Applications_sharing_one_cache_server_are_kept_apart_by_their_key_prefix()
    {
        var shared = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var appOne = Distributed(shared, "one:");
        var appTwo = Distributed(shared, "two:");

        await appOne.SetAsync("k", "from one", TimeSpan.FromMinutes(1));

        (await appTwo.GetAsync<string>("k")).Should().BeNull();
        appTwo.Remove("k");
        (await appOne.GetAsync<string>("k")).Should().Be("from one");
    }

    // ---- an unreachable cache is a miss, not an outage ----------------------------------------------------------------------------

    private sealed class BrokenCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("cache is down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("cache is down");
        public void Refresh(string key) => throw new InvalidOperationException("cache is down");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("cache is down");
        public void Remove(string key) => throw new InvalidOperationException("cache is down");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("cache is down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("cache is down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("cache is down");
    }

    [Fact]
    public async Task When_the_shared_cache_cannot_be_reached_reads_are_misses_and_the_work_still_gets_done()
    {
        var cache = Distributed(new BrokenCache());

        (await cache.GetAsync<string>("k")).Should().BeNull();
        await cache.Invoking(c => c.SetAsync("k", "v", TimeSpan.FromMinutes(1))).Should().NotThrowAsync();
        await cache.Invoking(c => c.RemoveAsync("k")).Should().NotThrowAsync();
        cache.Invoking(c => c.Remove("k")).Should().NotThrow();
        (await cache.GetOrAddAsync("k", _ => Task.FromResult("from the database"), TimeSpan.FromMinutes(1))).Should().Be("from the database");
    }

    // ---- choosing the backend by configuration (FR-CACHE-002) ----------------------------------------------------------------------

    private static ServiceProvider Register(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddPlatformCaching(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Memory_is_the_default_backend()
    {
        using var provider = Register([]);

        provider.GetRequiredService<ICacheService>().Should().BeOfType<MemoryCacheService>();
        provider.GetRequiredService<CacheOptions>().KeyPrefix.Should().Be("majds:");
    }

    [Fact]
    public void The_distributed_backend_is_chosen_by_configuration()
    {
        using var provider = Register(new() { ["Cache:Provider"] = "Distributed", ["Cache:KeyPrefix"] = "x:" });

        provider.GetRequiredService<ICacheService>().Should().BeOfType<DistributedCacheService>();
        provider.GetRequiredService<CacheOptions>().KeyFor("k").Should().Be("x:k");
    }

    [Fact]
    public void Redis_is_chosen_by_configuration_and_needs_a_connection_string()
    {
        // No connection is opened until the first use, so this proves the wiring without a Redis server.
        using var provider = Register(new() { ["Cache:Provider"] = "Redis", ["Cache:Redis:ConnectionString"] = "localhost:6379" });

        provider.GetRequiredService<ICacheService>().Should().BeOfType<DistributedCacheService>();
        provider.GetRequiredService<IDistributedCache>().GetType().Name.Should().Contain("Redis");

        ((Action)(() => Register(new() { ["Cache:Provider"] = "Redis" }))).Should().Throw<InvalidOperationException>().WithMessage("*ConnectionString*");
    }

    [Fact]
    public void An_unknown_provider_is_refused_at_startup_rather_than_silently_ignored()
    {
        ((Action)(() => Register(new() { ["Cache:Provider"] = "Memcached" }))).Should().Throw<InvalidOperationException>().WithMessage("*not supported*");
    }
}
