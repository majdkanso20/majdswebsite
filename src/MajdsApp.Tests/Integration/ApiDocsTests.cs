using System.Net;
using System.Text.Json;
using Asp.Versioning;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record DocsPermissionGroup(string Name, List<string> Permissions);

/// <summary>A controller that exists only in the test host, marked as version 2.0 to prove a new version documents itself.</summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/test-v2")]
public class TestVersionTwoController : ControllerBase
{
    [HttpGet("ping")]
    public string Ping() => "pong";
}

public class DocsEnabledFactory : ApiFactory
{
    public DocsEnabledFactory() : base(new Dictionary<string, string> { ["Docs:Enabled"] = "true" }, null) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(TestVersionTwoController).Assembly));
    }
}

public class DocsAuthenticatedFactory : ApiFactory
{
    public DocsAuthenticatedFactory() : base(new Dictionary<string, string> { ["Docs:Enabled"] = "true", ["Docs:Access"] = "Authenticated" }, null) { }
}

public class DocsOpenFactory : ApiFactory
{
    public DocsOpenFactory() : base(new Dictionary<string, string> { ["Docs:Enabled"] = "true", ["Docs:Access"] = "Open" }, null) { }
}

/// <summary>F-ApiDocs: the OpenAPI documents, bearer auth for trying endpoints, versions, and who may read the docs outside Development.</summary>
public class ApiDocsTests
{
    private static bool HasPath(JsonDocument doc, string path) =>
        doc.RootElement.GetProperty("paths").EnumerateObject().Any(p => p.Name.Equals(path, StringComparison.OrdinalIgnoreCase));

    private static async Task<JsonDocument> DocAsync(HttpClient http, string version = "v1") =>
        JsonDocument.Parse(await http.GetStringAsync($"/swagger/{version}/swagger.json"));

    public class Default(ApiFactory factory) : IClassFixture<ApiFactory>
    {
        [Fact]
        public async Task Outside_development_the_docs_are_off_unless_they_are_switched_on()
        {
            var admin = await factory.SignInAsync("docs.admin0@example.com", "Admin");

            (await factory.Anonymous().Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await admin.Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);   // even an administrator: it is off
            (await admin.Http.GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    public class Restricted(DocsEnabledFactory factory) : IClassFixture<DocsEnabledFactory>
    {
        [Fact]
        public async Task By_default_only_a_signed_in_user_with_the_docs_permission_can_read_them()
        {
            var admin = await factory.SignInAsync("docs.admin1@example.com", "Admin");
            var plain = await factory.SignInAsync("docs.plain1@example.com");

            (await factory.Anonymous().Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await plain.Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await admin.Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await plain.Http.GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.Forbidden);          // the UI is guarded too
        }

        [Fact]
        public async Task The_docs_permission_can_be_granted_like_any_other()
        {
            var admin = await factory.SignInAsync("docs.admin2@example.com", "Admin");
            var reader = await factory.SignInAsync("docs.reader@example.com");

            var tree = (await admin.GetAsync<List<DocsPermissionGroup>>("/api/permissions/tree")).Data!;
            tree.SelectMany(g => g.Permissions).Should().Contain("Docs.View");

            var role = await admin.PostAsync<string>("/api/roles/create", new { name = "DocReaders", displayName = "Doc readers", isDefault = false });
            await admin.PostAsync("/api/roles/permissions/update", new { roleId = role.Data, permissionNames = new[] { "Docs.View" } });
            var target = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=docs.reader")).Data!.Items.Single();
            await admin.PostAsync("/api/users/update", new { userId = target.Id, fullName = "R", phoneNumber = (string?)null, roles = new[] { "User", "DocReaders" } });

            (await reader.Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task The_document_covers_the_endpoints_describes_the_response_envelope_and_declares_bearer_auth()
        {
            var admin = await factory.SignInAsync("docs.admin3@example.com", "Admin");

            using var doc = await DocAsync(admin.Http);

            // FR-DOC-001 / AC-DOC-1: endpoints from several modules, including recent ones, with no documentation written by hand.
            foreach (var path in new[] { "/api/users/list", "/api/dashboard/widgets", "/api/exports/start", "/api/jobs/queue", "/api/plugins/install", "/api/localization/languages" })
                HasPath(doc, path).Should().BeTrue($"{path} should be documented");

            // The responses are described as the ResponseDto envelope.
            doc.RootElement.GetProperty("components").GetProperty("schemas").EnumerateObject().Select(p => p.Name).Should().Contain(n => n.EndsWith("ResponseDto"));

            // AC-DOC-2: a bearer scheme, and a requirement that applies it, so "Try it out" sends the token.
            doc.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString().Should().Be("bearer");
            doc.RootElement.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task The_interactive_page_offers_every_version_and_keeps_the_entered_token()
        {
            var admin = await factory.SignInAsync("docs.admin4@example.com", "Admin");

            var html = await admin.Http.GetStringAsync("/swagger/index.html");
            var config = await admin.Http.GetStringAsync("/swagger/index.js"); // the page loads its configuration from here

            html.Should().Contain("swagger");
            config.Should().Contain("/swagger/v1/swagger.json").And.Contain("/swagger/v2/swagger.json").And.Contain("\"persistAuthorization\":true");
        }
    }

    public class Authenticated(DocsAuthenticatedFactory factory) : IClassFixture<DocsAuthenticatedFactory>
    {
        [Fact]
        public async Task Access_can_be_relaxed_to_any_signed_in_user()
        {
            var plain = await factory.SignInAsync("docs.plain5@example.com");

            (await plain.Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await factory.Anonymous().Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    public class Open(DocsOpenFactory factory) : IClassFixture<DocsOpenFactory>
    {
        [Fact]
        public async Task Access_can_be_opened_to_everyone()
        {
            (await factory.Anonymous().Http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    // ---- versioning (FR-DOC-003) -------------------------------------------------------------------------------------------------

    public class Versions(DocsEnabledFactory factory) : IClassFixture<DocsEnabledFactory>
    {
        [Fact]
        public async Task Existing_routes_keep_working_with_no_version_and_say_which_versions_they_support()
        {
            var admin = await factory.SignInAsync("docs.admin6@example.com", "Admin");

            var response = await admin.Http.GetAsync("/api/dashboard/widgets");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.GetValues("api-supported-versions").Single().Should().Contain("1.0");
        }

        [Fact]
        public async Task A_version_can_be_named_in_the_query_or_in_a_header_and_an_unknown_one_is_refused()
        {
            var admin = await factory.SignInAsync("docs.admin7@example.com", "Admin");

            (await admin.Http.GetAsync("/api/dashboard/widgets?api-version=1.0")).StatusCode.Should().Be(HttpStatusCode.OK);

            using var withHeader = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/widgets");
            withHeader.Headers.Add("X-Api-Version", "1.0");
            (await admin.Http.SendAsync(withHeader)).StatusCode.Should().Be(HttpStatusCode.OK);

            (await admin.Http.GetAsync("/api/dashboard/widgets?api-version=9.0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task A_controller_marked_with_a_new_version_gets_its_own_document_and_is_reached_by_naming_that_version()
        {
            var admin = await factory.SignInAsync("docs.admin8@example.com", "Admin");

            using var v2 = await DocAsync(admin.Http, "v2");
            HasPath(v2, "/api/test-v2/ping").Should().BeTrue();
            v2.RootElement.GetProperty("info").GetProperty("version").GetString().Should().Be("2.0");

            using var v1 = await DocAsync(admin.Http, "v1");
            HasPath(v1, "/api/test-v2/ping").Should().BeFalse();     // each version documents only its own
            HasPath(v1, "/api/users/list").Should().BeTrue();

            (await admin.Http.GetAsync("/api/test-v2/ping?api-version=2.0")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.Http.GetAsync("/api/test-v2/ping")).StatusCode.Should().Be(HttpStatusCode.BadRequest);     // no version means 1.0, which it does not offer
        }
    }
}
