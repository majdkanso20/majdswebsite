using System.Net;
using FluentAssertions;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>Controllers that exist only in the test host, guarded declaratively (no MediatR request involved).</summary>
[ApiController]
[Route("api/test-guarded")]
[RequiresPermission("Roles.View")]
public class TestGuardedController : ApiControllerBase
{
    [HttpGet("whole-controller")]
    public ResponseDto<string> WholeController() => Ok("controller");

    [HttpGet("one-action")]
    [RequiresPermission("Users.Delete")]
    public ResponseDto<string> OneAction() => Ok("action");
}

public class GuardedFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(TestGuardedController).Assembly));
    }
}

/// <summary>F-Authorization FR-AUTHZ-004: [RequiresPermission] on a controller or an action is enforced by a filter, with 401 and 403 as ResponseDto.</summary>
public class PermissionActionFilterTests(GuardedFactory factory) : IClassFixture<GuardedFactory>
{
    [Fact]
    public async Task Nobody_signed_in_is_unauthorized()
    {
        (await factory.Anonymous().Http.GetAsync("/api/test-guarded/whole-controller")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_user_without_the_permission_gets_403_naming_it_in_the_standard_envelope()
    {
        var plain = await factory.SignInAsync("paf.plain@example.com");

        var controller = await plain.GetAsync<string>("/api/test-guarded/whole-controller");
        var action = await plain.GetAsync<string>("/api/test-guarded/one-action");

        controller.Status.Should().Be(HttpStatusCode.Forbidden);
        controller.Body!.Message.Should().Contain("Roles.View");
        action.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_action_needs_its_own_permission_as_well_as_the_controllers()
    {
        var admin = await factory.SignInAsync("paf.admin1@example.com", "Admin");
        var viewer = await factory.SignInAsync("paf.viewer@example.com");
        var role = await admin.PostAsync<string>("/api/roles/create", new { name = "PafViewers", displayName = "Viewers", isDefault = false });
        await admin.PostAsync("/api/roles/permissions/update", new { roleId = role.Data, permissionNames = new[] { "Roles.View" } });
        var target = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=paf.viewer")).Data!.Items.Single();
        await admin.PostAsync("/api/users/update", new { userId = target.Id, fullName = "V", phoneNumber = (string?)null, roles = new[] { "User", "PafViewers" } });

        (await viewer.GetAsync<string>("/api/test-guarded/whole-controller")).Status.Should().Be(HttpStatusCode.OK);   // the controller's permission is enough here
        (await viewer.GetAsync<string>("/api/test-guarded/one-action")).Status.Should().Be(HttpStatusCode.Forbidden);  // this action also needs Users.Delete
    }

    [Fact]
    public async Task An_administrator_passes_both()
    {
        var admin = await factory.SignInAsync("paf.admin2@example.com", "Admin");

        (await admin.GetAsync<string>("/api/test-guarded/one-action")).Data.Should().Be("action");
    }
}
