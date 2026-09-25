using System.Net;
using FluentAssertions;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>The whole application on the distributed cache path (values stored as JSON, as they would be in Redis): what is cached must
/// survive serialization, and a write must invalidate what it changed (F-Caching FR-CACHE-003, AC-CACHE-2).</summary>
public class DistributedCacheHostTests : IClassFixture<DistributedCacheHostTests.DistributedFactory>
{
    public class DistributedFactory : ApiFactory
    {
        public DistributedFactory() : base(new Dictionary<string, string> { ["Cache:Provider"] = "Distributed" }, null) { }
    }

    private readonly DistributedFactory _factory;
    public DistributedCacheHostTests(DistributedFactory factory) => _factory = factory;

    [Fact]
    public void The_host_really_runs_on_the_distributed_backend()
    {
        _factory.Services.GetRequiredService<ICacheService>().Should().BeOfType<DistributedCacheService>();
    }

    [Fact]
    public async Task A_changed_setting_is_visible_on_the_next_read_even_though_it_was_cached()
    {
        var admin = await _factory.SignInAsync("dc.admin1@example.com", "Admin");
        var reader = await _factory.SignInAsync("dc.reader1@example.com");
        Task<string> NameAsync() => reader.GetAsync<Dictionary<string, string>>("/api/settings/public").ContinueWith(t => t.Result.Data!["General.ApplicationName"]);

        (await NameAsync()).Should().Be("Majd's App");                                    // read once: now cached
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "General.ApplicationName", value = "Acme Portal" } } });

        (await NameAsync()).Should().Be("Acme Portal");                                   // the write invalidated it
    }

    [Fact]
    public async Task A_users_own_setting_and_their_effective_permissions_stay_correct_through_the_cache()
    {
        var admin = await _factory.SignInAsync("dc.admin2@example.com", "Admin");
        var user = await _factory.SignInAsync("dc.user2@example.com");

        (await user.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);   // cached: no permission

        var role = await admin.PostAsync<string>("/api/roles/create", new { name = "DcViewers", displayName = "Viewers", isDefault = false });
        await admin.PostAsync("/api/roles/permissions/update", new { roleId = role.Data, permissionNames = new[] { "Users.View" } });
        var target = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=dc.user2")).Data!.Items.Single();
        await admin.PostAsync("/api/users/update", new { userId = target.Id, fullName = "U", phoneNumber = (string?)null, roles = new[] { "User", "DcViewers" } });

        (await user.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.OK);        // invalidated

        await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "Appearance.Currency", value = "JOD" } } });
        (await user.GetAsync<Dictionary<string, string>>("/api/settings/public")).Data!["Appearance.Currency"].Should().Be("JOD");
    }

    [Fact]
    public async Task A_feature_switched_off_takes_effect_at_once()
    {
        var admin = await _factory.SignInAsync("dc.admin3@example.com", "Admin");
        var enabled = (await admin.GetAsync<List<string>>("/api/features/enabled")).Data!;
        enabled.Should().Contain("Files");

        (await admin.PostAsync("/api/features/set", new { name = "Files", enabled = false })).Status.Should().Be(HttpStatusCode.OK);

        (await admin.GetAsync<List<string>>("/api/features/enabled")).Data!.Should().NotContain("Files");
        await admin.PostAsync("/api/features/set", new { name = "Files", enabled = true });
    }
}
