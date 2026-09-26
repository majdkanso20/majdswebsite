using System.Net;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Notifications;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Roles FR-ROLE-003/004 (default role, reassignment on delete) and F-Account FR-ACC-003 (notification choices).</summary>
public class RolesAndSubscriptionsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record RoleRow(string Id, string Name, int UserCount);
    private record Subscription(string Type, bool InApp, bool Email);

    private static async Task<string> CreateRoleAsync(ApiClient admin, string name, bool isDefault = false) =>
        (await admin.PostAsync<string>("/api/roles/create", new { name, displayName = name, isDefault })).Data!;

    private static async Task GiveRoleAsync(ApiClient admin, string email, string role)
    {
        var user = (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=5&filter={email}")).Data!.Items.Single();
        await admin.PostAsync("/api/users/update", new { userId = user.Id, fullName = "U", phoneNumber = (string?)null, roles = new[] { "User", role } });
    }

    private static async Task<List<string>> RolesOfAsync(ApiClient admin, string email) =>
        (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=5&filter={email}")).Data!.Items.Single().Roles;

    [Fact]
    public async Task A_role_with_users_is_deleted_only_when_they_are_moved_to_another_role()
    {
        var admin = await factory.SignInAsync("rs.admin1@example.com", "Admin");
        await factory.SignInAsync("rs.member1@example.com");
        var doomed = await CreateRoleAsync(admin, "RsDoomed");
        var heir = await CreateRoleAsync(admin, "RsHeir");
        await GiveRoleAsync(admin, "rs.member1@example.com", "RsDoomed");

        (await admin.PostAsync("/api/roles/delete", new { roleId = doomed })).Status.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsync("/api/roles/delete", new { roleId = doomed, reassignToRoleId = doomed })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/roles/delete", new { roleId = doomed, reassignToRoleId = "no-such-role" })).Status.Should().Be(HttpStatusCode.NotFound);

        (await admin.PostAsync("/api/roles/delete", new { roleId = doomed, reassignToRoleId = heir })).Status.Should().Be(HttpStatusCode.OK);

        (await RolesOfAsync(admin, "rs.member1@example.com")).Should().Contain("RsHeir").And.NotContain("RsDoomed");
        (await admin.GetAsync<PagedData<RoleRow>>("/api/roles/list?page=1&pageSize=100")).Data!.Items.Should().NotContain(r => r.Name == "RsDoomed");
    }

    [Fact]
    public async Task A_new_account_gets_the_default_role_and_only_one_role_is_the_default()
    {
        var admin = await factory.SignInAsync("rs.admin2@example.com", "Admin");
        await CreateRoleAsync(admin, "RsFirstDefault", isDefault: true);
        await CreateRoleAsync(admin, "RsSecondDefault", isDefault: true);

        (await factory.Anonymous().PostAsync("/api/account/register", new { email = "rs.newcomer@example.com", password = ApiFactory.Password, fullName = "N" })).Status.Should().Be(HttpStatusCode.OK);

        var roles = await RolesOfAsync(admin, "rs.newcomer@example.com");
        roles.Should().Contain("RsSecondDefault").And.NotContain("RsFirstDefault");
        // Put the default back where the rest of the suite expects it.
        var user = (await admin.GetAsync<PagedData<RoleRow>>("/api/roles/list?page=1&pageSize=100")).Data!.Items.Single(r => r.Name == "User");
        await admin.PostAsync("/api/roles/update", new { roleId = user.Id, displayName = "User", isDefault = true });
    }

    [Fact]
    public async Task Notification_choices_reject_a_type_that_does_not_exist_and_the_default_is_stored_as_no_row()
    {
        var me = await factory.SignInAsync("rs.notify@example.com");

        (await me.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "Made-up", inApp = true, email = false } } })).Status.Should().Be(HttpStatusCode.BadRequest);

        await me.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "Security", inApp = true, email = false } } });
        (await me.GetAsync<List<Subscription>>("/api/notifications/subscriptions/get")).Data!.Single(s => s.Type == "Security").Email.Should().BeFalse();

        await me.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "Security", inApp = true, email = true } } });   // back to the default
        (await me.GetAsync<List<Subscription>>("/api/notifications/subscriptions/get")).Data!.Single(s => s.Type == "Security").Email.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<NotificationSubscription>().Where(s => s.Type == "Security").ToListAsync();
        rows.Should().BeEmpty("choosing every channel is the default, so nothing needs to be stored");
    }
}
