using FluentAssertions;
using MajdsApp.Modules.Notifications;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

public class RedisBackplaneFactory : ApiFactory
{
    // No connection is opened until the first hub use, so this proves the wiring without a Redis server (same approach as the cache's Redis test).
    public RedisBackplaneFactory() : base(new Dictionary<string, string> { ["SignalR:Redis:ConnectionString"] = "localhost:6399,abortConnect=false,connectTimeout=200,connectRetry=0" }, null) { }
}

/// <summary>NFR-SCALE-2: with <c>SignalR:Redis:ConnectionString</c> set, every node shares connected clients through Redis instead of keeping its own.</summary>
public class SignalRBackplaneTests(RedisBackplaneFactory factory) : IClassFixture<RedisBackplaneFactory>
{
    [Fact]
    public void The_hub_s_lifetime_manager_is_backed_by_Redis_when_a_connection_string_is_set()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<HubLifetimeManager<NotificationHub>>().GetType().Name.Should().Contain("Redis");
    }
}

/// <summary>With no connection string (the default), each node keeps its own connected clients: no Redis type, no error.</summary>
public class SingleNodeSignalRTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void The_hub_s_lifetime_manager_stays_in_process_with_no_Redis_configured()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<HubLifetimeManager<NotificationHub>>().GetType().Name.Should().NotContain("Redis");
    }
}
