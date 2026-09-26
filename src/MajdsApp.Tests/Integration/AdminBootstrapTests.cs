using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>The first administrator can be created from configuration, on an empty installation only.</summary>
public class AdminBootstrapTests
{
    private sealed class Empty(string email, string password) : ApiFactory(new Dictionary<string, string>
    {
        ["Bootstrap:AdminEmail"] = email, ["Bootstrap:AdminPassword"] = password
    }, null);

    [Fact]
    public async Task An_empty_installation_gets_the_configured_administrator_who_can_sign_in()
    {
        using var factory = new Empty("first.admin@example.com", "First-Admin1!");

        var login = await factory.CreateClient().PostAsync("/api/identity/login",
            System.Net.Http.Json.JsonContent.Create(new { email = "first.admin@example.com", password = "First-Admin1!" }));

        login.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.IsInRoleAsync((await users.FindByEmailAsync("first.admin@example.com"))!, "Admin")).Should().BeTrue();
    }

    [Fact]
    public async Task It_does_nothing_once_anyone_exists_so_it_cannot_bring_an_administrator_back()
    {
        using var factory = new Empty("late.admin@example.com", "Late-Admin1!");
        await factory.SignInAsync("someone.else@example.com");                       // a user exists
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.DeleteAsync((await users.FindByEmailAsync("late.admin@example.com"))!);   // the administrator is removed later
        }

        await MajdsApp.AdminBootstrap.RunAsync(factory.Services.CreateScope().ServiceProvider,
            factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        using var check = factory.Services.CreateScope();
        var rows = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.IgnoreQueryFilters().Where(u => u.Email == "late.admin@example.com").ToListAsync();
        rows.Should().ContainSingle().Which.IsDeleted.Should().BeTrue("the removed administrator stays removed; nothing was created again");
    }

    [Fact]
    public async Task Without_configuration_nothing_is_created()
    {
        using var factory = new ApiFactory();
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }
}
