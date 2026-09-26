using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Audit FR-AUDIT-006: requests refused before a handler runs (no token, wrong rights) are recorded too, once, and not flooded.</summary>
public class RefusedRequestAuditTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record Entry(string Action, string? UserName, string Outcome, bool Succeeded, string? Url);

    private static async Task<List<Entry>> EntriesForAsync(ApiClient admin, string path) =>
        (await admin.GetAsync<PagedData<Entry>>("/api/audit/list?page=1&pageSize=100&sort=createdAt:desc")).Data!.Items.Where(e => e.Url == path).ToList();

    [Fact]
    public async Task A_request_with_no_sign_in_is_recorded_as_unauthorized_once_however_often_it_is_repeated()
    {
        var admin = await factory.SignInAsync("ra.admin1@example.com", "Admin");

        for (var i = 0; i < 3; i++)
            (await factory.Anonymous().Http.GetAsync("/api/exports/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var entries = await EntriesForAsync(admin, "/api/exports/list");
        entries.Should().ContainSingle().Which.Should().Match<Entry>(e => e.Outcome == "Unauthorized" && !e.Succeeded && e.UserName == null);
    }

    [Fact]
    public async Task A_signed_in_user_refused_by_a_handler_is_recorded_once_not_twice()
    {
        var admin = await factory.SignInAsync("ra.admin2@example.com", "Admin");
        var plain = await factory.SignInAsync("ra.plain2@example.com");

        (await plain.GetAsync<object>("/api/jobs/queue?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);

        var entries = await EntriesForAsync(admin, "/api/jobs/queue");
        entries.Should().ContainSingle(e => e.UserName == "ra.plain2@example.com" && e.Outcome == "Forbidden");
    }

    [Fact]
    public async Task Requests_that_are_not_refused_or_are_not_api_calls_leave_no_refusal_entry()
    {
        var admin = await factory.SignInAsync("ra.admin3@example.com", "Admin");

        (await admin.GetAsync<object>("/api/settings/my")).Status.Should().Be(HttpStatusCode.OK);
        (await factory.Anonymous().Http.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.Anonymous().Http.GetAsync("/no/such/page")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);   // refused too (deny by default), but it is not an API call

        (await EntriesForAsync(admin, "/health/live")).Should().BeEmpty();
        (await EntriesForAsync(admin, "/no/such/page")).Should().BeEmpty();
    }
}
