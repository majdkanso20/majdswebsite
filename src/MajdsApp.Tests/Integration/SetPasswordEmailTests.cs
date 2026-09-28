using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

public class FakeAccountEmailSender : IEmailSender<ApplicationUser>
{
    public record Sent(string Email, string Link);

    public static readonly List<Sent> ResetLinks = [];

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) => Task.CompletedTask;

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        lock (ResetLinks) ResetLinks.Add(new Sent(email, resetLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) => Task.CompletedTask;
}

public class SetPasswordEmailFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddScoped<IEmailSender<ApplicationUser>, FakeAccountEmailSender>());
    }
}

/// <summary>F-Users FR-USER-002: either the admin sets a password directly, or the new account gets one
/// nobody knows and an emailed set-password link — the same mechanism Forgot password uses.</summary>
public class SetPasswordEmailTests(SetPasswordEmailFactory factory) : IClassFixture<SetPasswordEmailFactory>
{
    [Fact]
    public async Task No_password_is_required_when_a_set_password_email_is_requested_and_a_link_is_sent()
    {
        var admin = await factory.SignInAsync("spe.admin1@example.com", "Admin");
        FakeAccountEmailSender.ResetLinks.Clear();

        var response = await admin.PostAsync<string>("/api/users/create",
            new { email = "spe.newuser1@example.com", sendSetPasswordEmail = true, roles = Array.Empty<string>() });

        response.Status.Should().Be(HttpStatusCode.OK, string.Join("; ", response.Errors));
        var sent = FakeAccountEmailSender.ResetLinks.Should().ContainSingle(s => s.Email == "spe.newuser1@example.com").Which;
        sent.Link.Should().Contain("/reset-password").And.Contain("code=").And.Contain(Uri.EscapeDataString("spe.newuser1@example.com"));
    }

    [Fact]
    public async Task No_password_and_no_set_password_email_is_a_validation_error()
    {
        var admin = await factory.SignInAsync("spe.admin2@example.com", "Admin");

        var response = await admin.PostAsync<string>("/api/users/create",
            new { email = "spe.newuser2@example.com", sendSetPasswordEmail = false, roles = Array.Empty<string>() });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Setting_a_password_directly_still_works_and_sends_no_email()
    {
        var admin = await factory.SignInAsync("spe.admin3@example.com", "Admin");
        FakeAccountEmailSender.ResetLinks.Clear();

        var response = await admin.PostAsync<string>("/api/users/create",
            new { email = "spe.newuser3@example.com", password = ApiFactory.Password, sendSetPasswordEmail = false, roles = Array.Empty<string>() });

        response.Status.Should().Be(HttpStatusCode.OK, string.Join("; ", response.Errors));
        FakeAccountEmailSender.ResetLinks.Should().NotContain(s => s.Email == "spe.newuser3@example.com");

        // The password given at creation really is the account's: it can sign in with it.
        var signedIn = await factory.CreateClient().PostAsJsonAsync("/api/identity/login",
            new { email = "spe.newuser3@example.com", password = ApiFactory.Password });
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
