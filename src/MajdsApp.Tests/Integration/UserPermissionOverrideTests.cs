using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Authorization FR-AUTHZ-003: permissions can be granted or denied to one user directly, on top of what their roles give.</summary>
public class UserPermissionOverrideTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record Explained(string UserId, bool IsSuperAdmin, List<string> FromRoles, List<string> Granted, List<string> Denied, List<string> Effective);

    private async Task<string> IdOfAsync(ApiClient admin, string email) =>
        (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=5&filter={email}")).Data!.Items.Single().Id;

    [Fact]
    public async Task A_direct_grant_gives_one_user_a_permission_their_roles_do_not_and_takes_effect_at_once()
    {
        var admin = await factory.SignInAsync("upo.admin1@example.com", "Admin");
        var user = await factory.SignInAsync("upo.user1@example.com");
        var id = await IdOfAsync(admin, "upo.user1@example.com");
        (await user.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);

        (await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = new[] { "Users.View" }, denied = Array.Empty<string>() })).Status.Should().Be(HttpStatusCode.OK);

        (await user.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.OK);
        var explained = (await admin.GetAsync<Explained>($"/api/users/permissions/get?userId={id}")).Data!;
        explained.Granted.Should().Equal("Users.View");
        explained.FromRoles.Should().NotContain("Users.View");
        explained.Effective.Should().Contain("Users.View");
    }

    [Fact]
    public async Task A_direct_deny_removes_a_permission_the_roles_give_and_wins_over_a_grant_on_the_same_role()
    {
        var admin = await factory.SignInAsync("upo.admin2@example.com", "Admin");
        var user = await factory.SignInAsync("upo.user2@example.com");
        var id = await IdOfAsync(admin, "upo.user2@example.com");
        var role = await admin.PostAsync<string>("/api/roles/create", new { name = "UpoViewers", displayName = "Viewers", isDefault = false });
        await admin.PostAsync("/api/roles/permissions/update", new { roleId = role.Data, permissionNames = new[] { "Users.View" } });
        await admin.PostAsync("/api/users/update", new { userId = id, fullName = "U", phoneNumber = (string?)null, roles = new[] { "User", "UpoViewers" } });
        (await user.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.OK);

        await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = Array.Empty<string>(), denied = new[] { "Users.View" } });

        (await user.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);
        var explained = (await admin.GetAsync<Explained>($"/api/users/permissions/get?userId={id}")).Data!;
        explained.FromRoles.Should().Contain("Users.View");
        explained.Denied.Should().Equal("Users.View");
        explained.Effective.Should().NotContain("Users.View");
    }

    [Fact]
    public async Task Overrides_are_replaced_as_a_set_and_can_be_cleared()
    {
        var admin = await factory.SignInAsync("upo.admin3@example.com", "Admin");
        await factory.SignInAsync("upo.user3@example.com");
        var id = await IdOfAsync(admin, "upo.user3@example.com");
        await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = new[] { "Users.View", "Roles.View" }, denied = Array.Empty<string>() });

        await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = new[] { "Roles.View" }, denied = Array.Empty<string>() });
        (await admin.GetAsync<Explained>($"/api/users/permissions/get?userId={id}")).Data!.Granted.Should().Equal("Roles.View");

        await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = Array.Empty<string>(), denied = Array.Empty<string>() });
        var cleared = (await admin.GetAsync<Explained>($"/api/users/permissions/get?userId={id}")).Data!;
        cleared.Granted.Should().BeEmpty();
        cleared.Denied.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_or_contradictory_permissions_and_unknown_users_are_refused()
    {
        var admin = await factory.SignInAsync("upo.admin4@example.com", "Admin");
        await factory.SignInAsync("upo.user4@example.com");
        var id = await IdOfAsync(admin, "upo.user4@example.com");

        (await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = new[] { "Made.Up" }, denied = Array.Empty<string>() })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = new[] { "Users.View" }, denied = new[] { "Users.View" } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/users/permissions/update", new { userId = "nobody", granted = Array.Empty<string>(), denied = Array.Empty<string>() })).Status.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync<Explained>("/api/users/permissions/get?userId=nobody")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_people_who_may_edit_users_can_change_overrides_and_only_people_who_may_view_can_read_them()
    {
        var admin = await factory.SignInAsync("upo.admin5@example.com", "Admin");
        var plain = await factory.SignInAsync("upo.plain5@example.com");
        var id = await IdOfAsync(admin, "upo.plain5@example.com");

        (await plain.PostAsync("/api/users/permissions/update", new { userId = id, granted = new[] { "Users.Edit" }, denied = Array.Empty<string>() })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.GetAsync<Explained>($"/api/users/permissions/get?userId={id}")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_super_administrator_holds_everything_whatever_is_denied()
    {
        var admin = await factory.SignInAsync("upo.admin6@example.com", "Admin");
        var id = await IdOfAsync(admin, "upo.admin6@example.com");

        await admin.PostAsync("/api/users/permissions/update", new { userId = id, granted = Array.Empty<string>(), denied = new[] { "Users.View" } });

        (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<Explained>($"/api/users/permissions/get?userId={id}")).Data!.IsSuperAdmin.Should().BeTrue();
    }
}
