using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>
/// P4 FR-XC-005: request-level concerns are middleware, including response wrapping — every response under
/// <c>/api</c>, not just the ones a controller produced, carries the standard <c>ResponseDto</c> envelope.
/// </summary>
public class ResponseWrappingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task An_anonymous_request_refused_by_the_deny_by_default_policy_is_wrapped()
    {
        // No endpoint matches this path at all, so the fallback authorization policy (NFR-SEC-1) is the
        // only thing that runs before a status code is set — no controller, no exception, nothing else
        // to wrap the body.
        var response = await factory.CreateClient().GetAsync("/api/this-route-does-not-exist");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        body.Should().Contain("\"code\":401").And.Contain("Authentication is required");
    }

    [Fact]
    public async Task A_signed_in_request_to_a_path_nothing_maps_to_is_wrapped_as_404()
    {
        var admin = await factory.SignInAsync("wrap.admin1@example.com", "Admin");

        var response = await admin.Http.GetAsync("/api/this-route-does-not-exist");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        body.Should().Contain("\"code\":404").And.Contain("was not found");
    }

    [Fact]
    public async Task The_message_is_translated_like_every_other_server_message()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/this-route-does-not-exist");
        request.Headers.AcceptLanguage.ParseAdd("ar");

        var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().MatchRegex("[؀-ۿ]").And.NotContain("Authentication is required");
    }

    [Fact]
    public async Task A_404_outside_api_is_left_exactly_as_ASP_NET_would_send_it()
    {
        // A missing static asset, an unknown SignalR or Swagger path and the like are not this API's shape to keep.
        var admin = await factory.SignInAsync("wrap.admin3@example.com", "Admin");

        var response = await admin.Http.GetAsync("/this-is-not-an-api-path");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task A_response_a_controller_already_wrote_is_left_alone()
    {
        // The ordinary path: a real 404 from a handler (NotFoundException) is already the envelope, and this
        // middleware must not double-wrap or otherwise disturb it.
        var admin = await factory.SignInAsync("wrap.admin2@example.com", "Admin");

        var response = await admin.Http.GetAsync("/api/users/get?userId=00000000-0000-0000-0000-000000000000");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("\"code\":404");
    }
}
