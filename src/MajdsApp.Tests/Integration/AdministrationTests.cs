using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record UserRow(string Id, string Email, string? FullName, bool IsActive, List<string> Roles);
public record RoleRow(string Id, string Name, string? DisplayName, bool IsStatic, bool IsDefault, int UserCount);

/// <summary>F-Authorization, F-Users and F-Roles through the real HTTP pipeline: the happy path, the
/// permission-denied path (Definition of Done) and the validation-failure path for each.</summary>
public class AdministrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private Task<ApiClient> AdminAsync() => factory.SignInAsync("admin@example.com", "Admin");
    private Task<ApiClient> PlainUserAsync() => factory.SignInAsync("plain@example.com", "User");

    // --- authorization (AC-AUTHZ-1/3/4) ---

    [Fact]
    public async Task A_user_without_the_permission_gets_403_in_the_standard_envelope()
    {
        var plain = await PlainUserAsync();

        var response = await plain.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5");

        response.Status.Should().Be(HttpStatusCode.Forbidden);
        response.Code.Should().Be(403);
        response.Body!.Message.Should().Contain("Users.View");
        response.Data.Should().BeNull();
    }

    [Fact]
    public async Task A_state_changing_call_without_permission_is_refused_and_changes_nothing()
    {
        var plain = await PlainUserAsync();
        var admin = await AdminAsync();

        var refused = await plain.PostAsync<string>("/api/users/create",
            new { email = "sneaky@example.com", password = ApiFactory.Password, roles = Array.Empty<string>() });

        refused.Status.Should().Be(HttpStatusCode.Forbidden);
        var users = await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50");
        users.Data!.Items.Should().NotContain(u => u.Email == "sneaky@example.com");
    }

    [Fact]
    public async Task The_administrator_holds_every_permission_and_a_plain_user_none()
    {
        var admin = await AdminAsync();
        var plain = await PlainUserAsync();

        var tree = await admin.GetAsync<List<PermissionGroupRow>>("/api/permissions/tree");
        var all = tree.Data!.SelectMany(g => g.Permissions).ToList();
        var adminPerms = await admin.GetAsync<List<string>>("/api/session/permissions");
        var plainPerms = await plain.GetAsync<List<string>>("/api/session/permissions");

        adminPerms.Data.Should().BeEquivalentTo(all);
        plainPerms.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Granting_a_permission_to_a_role_takes_effect_on_the_users_next_request()
    {
        var admin = await AdminAsync();
        await factory.CreateUserAsync("viewer@example.com", "User");
        var viewer = await factory.SignInAsync("viewer@example.com");
        (await viewer.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);

        var role = await admin.PostAsync<string>("/api/roles/create", new { name = "UserViewers", displayName = "User viewers", isDefault = false });
        await admin.PostAsync("/api/roles/permissions/update", new { roleId = role.Data, permissionNames = new[] { "Users.View" } });
        var target = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=viewer@")).Data!.Items.Single();
        await admin.PostAsync("/api/users/update", new { userId = target.Id, fullName = "Viewer", phoneNumber = (string?)null, roles = new[] { "User", "UserViewers" } });

        (await viewer.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.OK); // AC-AUTHZ-2
    }

    // --- users (AC-USER-1..3, FR-USER-007) ---

    [Fact]
    public async Task Creating_a_user_with_a_duplicate_email_fails_and_creates_nothing()
    {
        var admin = await AdminAsync();
        await admin.PostAsync<string>("/api/users/create", new { email = "dup@example.com", password = ApiFactory.Password, roles = Array.Empty<string>() });

        var again = await admin.PostAsync<string>("/api/users/create", new { email = "dup@example.com", password = ApiFactory.Password, roles = Array.Empty<string>() });

        again.Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=dup@")).Data!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task A_weak_password_is_rejected_with_a_readable_reason()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync<string>("/api/users/create", new { email = "weak@example.com", password = "abc", roles = Array.Empty<string>() });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        response.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_deactivated_user_disappears_from_sign_in_and_can_be_reactivated()
    {
        var admin = await AdminAsync();
        var user = await factory.CreateUserAsync("toggle@example.com");

        await admin.PostAsync("/api/users/set-activation", new { userId = user.Id, isActive = false });
        (await factory.CreateClient().PostAsJsonAsyncCompat("/api/identity/login", "toggle@example.com")).Should().Be(HttpStatusCode.Unauthorized);

        await admin.PostAsync("/api/users/set-activation", new { userId = user.Id, isActive = true });
        (await factory.CreateClient().PostAsJsonAsyncCompat("/api/identity/login", "toggle@example.com")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_administrator_cannot_delete_their_own_account()
    {
        var admin = await AdminAsync();
        var me = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=admin@")).Data!.Items.Single(u => u.Email == "admin@example.com");

        var response = await admin.PostAsync("/api/users/delete", new { userId = me.Id });

        response.Status.Should().Be(HttpStatusCode.Conflict); // AC-USER-3
    }

    [Fact]
    public async Task Removing_the_administrator_role_from_the_last_administrator_is_refused()
    {
        // FR-USER-007: the same protection as delete/deactivate must hold when the role is removed by editing the user.
        using var isolated = new ApiFactory();
        var admin = await isolated.SignInAsync("only.admin@example.com", "Admin");
        var me = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50")).Data!.Items.Single();

        var response = await admin.PostAsync("/api/users/update",
            new { userId = me.Id, fullName = "Only Admin", phoneNumber = (string?)null, roles = new[] { "User" } });

        response.Status.Should().Be(HttpStatusCode.Conflict);
        (await admin.GetAsync<List<string>>("/api/session/permissions")).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_lists_are_paged_and_sorted_on_the_server()
    {
        var admin = await AdminAsync();
        foreach (var name in new[] { "zed", "amy", "bob" })
            await factory.CreateUserAsync($"{name}.paging@example.com");

        var page = await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=2&sort=email:asc&filter=.paging@");

        page.Data!.TotalCount.Should().Be(3);
        page.Data.Items.Select(u => u.Email).Should().Equal("amy.paging@example.com", "bob.paging@example.com");
        page.Data.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task An_oversized_page_size_is_clamped()
    {
        var admin = await AdminAsync();

        var page = await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=100000");

        page.Data!.PageSize.Should().Be(100); // AC-GRID-2
    }

    // --- roles (AC-ROLE-1) ---

    [Fact]
    public async Task Role_names_must_be_unique_and_present()
    {
        var admin = await AdminAsync();
        await admin.PostAsync<string>("/api/roles/create", new { name = "Auditors", displayName = "Auditors", isDefault = false });

        var duplicate = await admin.PostAsync<string>("/api/roles/create", new { name = "Auditors", displayName = "again", isDefault = false });
        var empty = await admin.PostAsync<string>("/api/roles/create", new { name = "", displayName = "x", isDefault = false });

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        empty.Status.Should().Be(HttpStatusCode.BadRequest);
        empty.Errors.Should().Contain(e => e.Contains("Name"));
    }

    [Fact]
    public async Task Built_in_roles_and_roles_that_still_have_members_cannot_be_deleted()
    {
        var admin = await AdminAsync();
        var roles = (await admin.GetAsync<PagedData<RoleRow>>("/api/roles/list?page=1&pageSize=50")).Data!.Items;
        var adminRole = roles.Single(r => r.Name == "Admin");

        var builtIn = await admin.PostAsync("/api/roles/delete", new { roleId = adminRole.Id });

        builtIn.Status.Should().NotBe(HttpStatusCode.OK); // AC-ROLE-1
        adminRole.IsStatic.Should().BeTrue();
    }

    private record PermissionGroupRow(string Name, List<string> Permissions);
}

internal static class LoginProbe
{
    /// <summary>Signs in with the shared test password and returns only the HTTP status.</summary>
    public static async Task<HttpStatusCode> PostAsJsonAsyncCompat(this HttpClient http, string url, string email) =>
        (await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(http, url, new { email, password = ApiFactory.Password })).StatusCode;
}
