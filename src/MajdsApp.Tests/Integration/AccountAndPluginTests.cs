using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>Self-service account recovery and registration (F-Account).</summary>
public class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Forgot_password_answers_identically_whether_or_not_the_account_exists()
    {
        await factory.CreateUserAsync("exists@example.com");
        var anonymous = factory.Anonymous();

        var known = await anonymous.PostAsync("/api/account/forgot-password", new { email = "exists@example.com" });
        var unknown = await anonymous.PostAsync("/api/account/forgot-password", new { email = "ghost@example.com" });

        known.Status.Should().Be(HttpStatusCode.OK);
        unknown.Status.Should().Be(known.Status);
        unknown.Body!.Message.Should().Be(known.Body!.Message);   // no account enumeration
    }

    [Fact]
    public async Task Forgot_password_rejects_a_malformed_email()
    {
        var response = await factory.Anonymous().PostAsync("/api/account/forgot-password", new { email = "not-an-email" });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        response.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_reset_with_a_bad_link_is_refused_with_a_readable_message()
    {
        await factory.CreateUserAsync("reset.target@example.com");

        var bogus = await factory.Anonymous().PostAsync("/api/account/reset-password",
            new { email = "reset.target@example.com", code = "garbage", newPassword = "NewPass123!" });
        var unknownAccount = await factory.Anonymous().PostAsync("/api/account/reset-password",
            new { email = "ghost@example.com", code = "garbage", newPassword = "NewPass123!" });

        bogus.Status.Should().Be(HttpStatusCode.BadRequest);
        bogus.Errors.Should().Contain("This reset link is invalid or has expired.");
        unknownAccount.Errors.Should().Contain("This reset link is invalid or has expired."); // same answer: no enumeration
    }

    [Fact]
    public async Task Registration_creates_an_unconfirmed_account_that_cannot_sign_in_yet()
    {
        var response = await factory.Anonymous().PostAsync("/api/account/register",
            new { email = "newcomer@example.com", password = ApiFactory.Password, fullName = "New Comer" });
        var login = await factory.CreateClient().PostAsync("/api/identity/login",
            System.Net.Http.Json.JsonContent.Create(new { email = "newcomer@example.com", password = ApiFactory.Password }));

        response.Status.Should().Be(HttpStatusCode.OK);
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // must confirm the email first
    }

    [Fact]
    public async Task Registration_can_be_switched_off_by_an_administrator()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.SignInAsync("admin@example.com", "Admin");
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Security.AllowSelfRegistration", value = "false" } } });

        var open = await isolated.Anonymous().GetAsync<bool>("/api/account/registration-open");
        var attempt = await isolated.Anonymous().PostAsync("/api/account/register",
            new { email = "late@example.com", password = ApiFactory.Password, fullName = "Late" });

        open.Data.Should().BeFalse();
        attempt.Status.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_user_can_change_their_own_password_only_with_the_current_one()
    {
        var user = await factory.SignInAsync("changer@example.com");

        var wrong = await user.PostAsync("/api/account/change-password", new { currentPassword = "not-my-password", newPassword = "Another1234!" });
        var right = await user.PostAsync("/api/account/change-password", new { currentPassword = ApiFactory.Password, newPassword = "Another1234!" });

        wrong.Status.Should().Be(HttpStatusCode.BadRequest);   // AC-ACCT-1
        right.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Profile_endpoints_only_ever_touch_the_callers_own_record()
    {
        var alice = await factory.SignInAsync("alice.p@example.com");
        var bob = await factory.SignInAsync("bob.p@example.com");

        await alice.PostAsync("/api/account/update-profile", new { fullName = "Alice Changed", phoneNumber = (string?)null });

        (await alice.GetAsync<ProfileRow>("/api/account/me")).Data!.FullName.Should().Be("Alice Changed");
        (await bob.GetAsync<ProfileRow>("/api/account/me")).Data!.FullName.Should().NotBe("Alice Changed"); // AC-ACCT-3
    }

    private record ProfileRow(string Email, string? FullName);
}

/// <summary>A host with no plugins folder content must simply have no plugins, never fail to start.</summary>
public class NoPluginsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task An_empty_plugins_folder_means_an_empty_list()
    {
        var admin = await factory.SignInAsync("admin@example.com", "Admin");

        var response = await admin.GetAsync<List<object>>("/api/plugins/list");

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Data.Should().BeEmpty();
    }
}
