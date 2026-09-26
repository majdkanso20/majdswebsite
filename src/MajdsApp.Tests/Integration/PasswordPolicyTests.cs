using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Users FR-USER-008: the administrator sets the minimum password length and it applies wherever a password is set.</summary>
public class PasswordPolicyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static object Setting(string value) => new { items = new[] { new { name = "Security.MinPasswordLength", value } } };

    [Fact]
    public async Task A_longer_minimum_stops_shorter_passwords_when_creating_a_user_and_when_changing_ones_own()
    {
        var admin = await factory.SignInAsync("pw.admin1@example.com", "Admin");
        var me = await factory.SignInAsync("pw.me1@example.com");
        try
        {
            (await admin.PostAsync("/api/settings/update", Setting("12"))).Status.Should().Be(HttpStatusCode.OK);

            var tooShort = await admin.PostAsync("/api/users/create", new { email = "pw.new1@example.com", password = "Abcdef1!", roles = Array.Empty<string>() });
            tooShort.Status.Should().Be(HttpStatusCode.BadRequest);
            string.Join(" ", tooShort.Errors).Should().Contain("at least 12 characters");

            var ok = await admin.PostAsync("/api/users/create", new { email = "pw.new2@example.com", password = "Abcdef1!Abcdef1!", roles = Array.Empty<string>() });
            ok.Status.Should().Be(HttpStatusCode.OK);

            (await me.PostAsync("/api/account/change-password", new { currentPassword = ApiFactory.Password, newPassword = "Newpass1!" })).Status.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await admin.PostAsync("/api/settings/update", Setting("6"));
        }
    }

    [Fact]
    public async Task The_default_minimum_is_still_six_and_the_setting_is_checked_when_saved()
    {
        var admin = await factory.SignInAsync("pw.admin2@example.com", "Admin");

        (await admin.PostAsync("/api/users/create", new { email = "pw.new3@example.com", password = "Abc1!x", roles = Array.Empty<string>() })).Status.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync("/api/settings/update", Setting("3"))).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", Setting("500"))).Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
