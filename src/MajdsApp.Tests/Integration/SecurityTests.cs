using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>NFR-SEC-1/2/4: deny by default, hardened responses, and rate limiting on the auth endpoints.</summary>
public class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/api/settings/public")]
    [InlineData("/api/settings/list")]
    [InlineData("/api/users/list")]
    [InlineData("/api/search?q=a")]
    [InlineData("/api/plugins/manifest")]
    [InlineData("/api/notifications/list")]
    [InlineData("/api/audit/list")]
    [InlineData("/api/permissions/tree")]
    [InlineData("/api/identity/manage/info")]
    public async Task Anonymous_requests_to_protected_endpoints_are_rejected(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/api/account/registration-open")]
    public async Task Public_endpoints_stay_public(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_notification_hub_requires_a_token()
    {
        var response = await factory.CreateClient().PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Responses_carry_the_hardening_headers()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }

    [Fact]
    public async Task A_valid_login_returns_a_bearer_token_and_a_wrong_password_does_not()
    {
        await factory.CreateUserAsync("login.check@example.com");

        var good = await factory.CreateClient().PostAsJsonAsync("/api/identity/login",
            new { email = "login.check@example.com", password = ApiFactory.Password });
        var bad = await factory.CreateClient().PostAsJsonAsync("/api/identity/login",
            new { email = "login.check@example.com", password = "wrong-password" });

        good.StatusCode.Should().Be(HttpStatusCode.OK);
        (await good.Content.ReadAsStringAsync()).Should().Contain("accessToken");
        bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_sign_in()
    {
        await factory.CreateUserAsync("inactive@example.com", active: false);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/identity/login",
            new { email = "inactive@example.com", password = ApiFactory.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // AC-USER-2
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task Repeated_login_attempts_are_throttled_with_the_standard_envelope()
    {
        using var factory = new ApiFactory(new Dictionary<string, string> { ["RateLimiting:AuthPermitLimit"] = "3" }, null);
        var http = factory.CreateClient();
        var attempt = () => http.PostAsJsonAsync("/api/identity/login", new { email = "nobody@example.com", password = "x" });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) statuses.Add((await attempt()).StatusCode);

        statuses.Take(3).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized);
        statuses.Skip(3).Should().OnlyContain(s => s == HttpStatusCode.TooManyRequests);

        var throttled = await attempt();
        throttled.Headers.Contains("Retry-After").Should().BeTrue();
        (await throttled.Content.ReadAsStringAsync()).Should().Contain("\"code\":429");
    }

    [Fact]
    public async Task Forgot_password_shares_the_auth_limit()
    {
        using var factory = new ApiFactory(new Dictionary<string, string> { ["RateLimiting:AuthPermitLimit"] = "2" }, null);
        var http = factory.CreateClient();

        for (var i = 0; i < 2; i++)
            (await http.PostAsJsonAsync("/api/account/forgot-password", new { email = "a@b.co" })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await http.PostAsJsonAsync("/api/account/forgot-password", new { email = "a@b.co" })).StatusCode
            .Should().Be(HttpStatusCode.TooManyRequests);
    }
}
