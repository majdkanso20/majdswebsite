using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MajdsApp.Configuration;
using MajdsApp.Data;
using MajdsApp.Modules.Health;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record HealthCheckRow(string Name, string Status, string? Description);
public record HealthReportRow(string Status, List<HealthCheckRow> Checks);

/// <summary>A host with one extra check, so readiness can be seen going Degraded and Unhealthy.</summary>
public class HealthFactory : ApiFactory
{
    public static volatile HealthStatus Extra = HealthStatus.Healthy;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddHealthChecks()
            .AddCheck("test-dependency", () => new HealthCheckResult(Extra, "test dependency")));
    }
}

/// <summary>F-Health: liveness, readiness (critical vs non-critical dependencies), metrics access, and correlation ids.</summary>
public class HealthAndObservabilityTests(HealthFactory factory) : IClassFixture<HealthFactory>
{
    private static async Task<(HttpStatusCode Status, HealthReportRow Report)> ReadinessAsync(HttpClient http)
    {
        var response = await http.GetAsync("/health/ready");
        var report = JsonSerializer.Deserialize<HealthReportRow>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return (response.StatusCode, report);
    }

    // ---- liveness and readiness (AC-HEALTH-2, FR-HEALTH-001) ----------------------------------------------------------

    [Fact]
    public async Task Liveness_answers_healthy_without_running_any_dependency_check()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = JsonSerializer.Deserialize<HealthReportRow>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        report.Status.Should().Be("Healthy");
        report.Checks.Should().BeEmpty();
    }

    [Fact]
    public async Task Readiness_checks_the_database_storage_cache_and_email()
    {
        HealthFactory.Extra = HealthStatus.Healthy;

        var (status, report) = await ReadinessAsync(factory.CreateClient());

        status.Should().Be(HttpStatusCode.OK);
        report.Status.Should().Be("Healthy");
        report.Checks.Select(c => c.Name).Should().Contain(["database", "file-storage", "cache", "email"]);
        report.Checks.Single(c => c.Name == "cache").Description.Should().Contain("Memory cache is working");
        report.Checks.Single(c => c.Name == "email").Description.Should().Contain("not configured");   // no SMTP credentials in the test host
    }

    [Fact]
    public async Task A_critical_dependency_that_is_down_makes_readiness_unhealthy_with_a_503()
    {
        HealthFactory.Extra = HealthStatus.Unhealthy;
        try
        {
            var (status, report) = await ReadinessAsync(factory.CreateClient());

            status.Should().Be(HttpStatusCode.ServiceUnavailable);                                    // FR-HEALTH-004
            report.Status.Should().Be("Unhealthy");
            (await factory.CreateClient().GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);   // the process itself is still alive
        }
        finally
        {
            HealthFactory.Extra = HealthStatus.Healthy;
        }
    }

    [Fact]
    public async Task A_dependency_the_platform_can_work_without_only_degrades_readiness()
    {
        HealthFactory.Extra = HealthStatus.Degraded;
        try
        {
            var (status, report) = await ReadinessAsync(factory.CreateClient());

            status.Should().Be(HttpStatusCode.OK);
            report.Status.Should().Be("Degraded");
        }
        finally
        {
            HealthFactory.Extra = HealthStatus.Healthy;
        }
    }

    [Fact]
    public async Task An_unreachable_database_reports_unhealthy_AC_HEALTH_1()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=Z:/this/folder/does/not/exist/db.sqlite;Mode=ReadOnly").Options;
        await using var db = new ApplicationDbContext(options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    // ---- the individual checks -------------------------------------------------------------------------------------------------

    private sealed class ForgetfulCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken ct = default) => Task.CompletedTask;
        public Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken ct = default) => factory(ct);
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(string key) { }
    }

    [Fact]
    public async Task A_cache_that_does_not_return_what_was_stored_is_degraded_not_unhealthy()
    {
        var result = await new CacheHealthCheck(new ForgetfulCache(), new CacheOptions("Redis", "x:")).CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Redis");
    }

    private sealed class Sender : IEmailMessageSender
    {
        public Task<DeliveryOutcome> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(DeliveryOutcome.Sent);
    }

    private static EmailHealthCheck EmailCheck(string host, int port, bool configured)
    {
        var services = new ServiceCollection();
        if (configured) services.AddSingleton<IEmailMessageSender>(new Sender());
        return new EmailHealthCheck(services.BuildServiceProvider(),
            Microsoft.Extensions.Options.Options.Create(new SmtpOptions { Host = host, Port = port }), new DefaultSettingsProvider());
    }

    [Fact]
    public async Task Email_is_healthy_when_the_mail_server_accepts_a_connection_degraded_when_it_does_not_and_skipped_when_unconfigured()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        (await EmailCheck("127.0.0.1", port, configured: true).CheckHealthAsync(new HealthCheckContext())).Status.Should().Be(HealthStatus.Healthy);
        listener.Stop();
        (await EmailCheck("127.0.0.1", port, configured: true).CheckHealthAsync(new HealthCheckContext())).Status.Should().Be(HealthStatus.Degraded);
        (await EmailCheck("127.0.0.1", port, configured: false).CheckHealthAsync(new HealthCheckContext())).Description.Should().Contain("not configured");
    }

    // ---- correlation and tracing (FR-HEALTH-003) -----------------------------------------------------------------------------------

    [Fact]
    public async Task Every_response_carries_a_correlation_id_that_is_the_trace_id_unless_the_caller_supplied_one()
    {
        var http = factory.CreateClient();

        var generated = (await http.GetAsync("/health/live")).Headers.GetValues("X-Correlation-Id").Single();
        generated.Should().MatchRegex("^[0-9a-f]{32}$");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "order-4711.retry:2");
        (await http.SendAsync(request)).Headers.GetValues("X-Correlation-Id").Single().Should().Be("order-4711.retry:2");
    }

    [Theory]
    [InlineData("has spaces and <script>")]
    [InlineData("caf\u00e9")]
    public async Task A_correlation_id_that_could_forge_a_log_line_is_replaced(string bad)
    {
        var http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", bad);

        var used = (await http.SendAsync(request)).Headers.GetValues("X-Correlation-Id").Single();

        used.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task An_over_long_correlation_id_is_replaced()
    {
        var http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", new string('a', 101));

        (await http.SendAsync(request)).Headers.GetValues("X-Correlation-Id").Single().Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task A_trace_started_by_an_upstream_service_is_continued_and_its_id_becomes_the_correlation_id()
    {
        var http = factory.CreateClient();
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");

        (await http.SendAsync(request)).Headers.GetValues("X-Correlation-Id").Single().Should().Be(traceId);
    }

    [Fact]
    public async Task A_malformed_traceparent_is_ignored_instead_of_failing_the_request()
    {
        var http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("traceparent", "garbage");

        var response = await http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-Id").Single().Should().MatchRegex("^[0-9a-f]{32}$");
    }
}

/// <summary>/metrics: request rate, latency and errors, readable only by whoever is meant to scrape it (FR-HEALTH-002).</summary>
public partial class MetricsTests
{
    public class TokenFactory : ApiFactory
    {
        public TokenFactory() : base(new Dictionary<string, string> { ["Metrics:Token"] = "s3cret-scrape-token" }, null) { }
    }

    public class OpenFactory : ApiFactory
    {
        public OpenFactory() : base(new Dictionary<string, string> { ["Metrics:AllowAnonymous"] = "true" }, null) { }
    }

    public class OffFactory : ApiFactory
    {
        public OffFactory() : base(new Dictionary<string, string> { ["Metrics:Enabled"] = "false", ["Metrics:Token"] = "x" }, null) { }
    }

    public class TokenTests(TokenFactory factory) : IClassFixture<TokenFactory>
    {
        private static HttpRequestMessage Scrape(string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        [Fact]
        public async Task A_scraper_needs_the_token()
        {
            var http = factory.CreateClient();

            (await http.SendAsync(Scrape(null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await http.SendAsync(Scrape("wrong"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await http.SendAsync(Scrape("s3cret-scrape-token"))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Request_counts_by_status_code_and_latency_are_exposed_so_rate_latency_and_errors_can_be_charted()
        {
            var admin = await factory.SignInAsync("metrics.admin@example.com", "Admin");
            var plain = await factory.SignInAsync("metrics.plain@example.com");
            await admin.GetAsync<object>("/api/users/list?page=1&pageSize=5");                 // 200
            await plain.GetAsync<object>("/api/users/list?page=1&pageSize=5");                 // 403

            var body = await (await factory.CreateClient().SendAsync(Scrape("s3cret-scrape-token"))).Content.ReadAsStringAsync();

            body.Should().Contain("http_request_duration_seconds_count");                       // rate and latency
            body.Should().Contain("http_request_duration_seconds_bucket");
            body.Should().MatchRegex(@"http_request_duration_seconds_count\{[^}]*code=""200""[^}]*\} [1-9]");
            body.Should().MatchRegex(@"http_request_duration_seconds_count\{[^}]*code=""403""[^}]*\} [1-9]");   // error rate
            body.Should().Contain("process_cpu_seconds_total");                                 // runtime metrics come with it
        }
    }

    public class OpenTests(OpenFactory factory) : IClassFixture<OpenFactory>
    {
        [Fact]
        public async Task Anonymous_access_can_be_allowed_by_configuration()
        {
            (await factory.CreateClient().GetAsync("/metrics")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class OffTests(OffFactory factory) : IClassFixture<OffFactory>
    {
        [Fact]
        public async Task Metrics_can_be_switched_off()
        {
            (await factory.CreateClient().GetAsync("/metrics")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    public class DefaultTests(ApiFactory factory) : IClassFixture<ApiFactory>
    {
        [Fact]
        public async Task With_no_token_the_endpoint_does_not_exist_outside_development()
        {
            (await factory.CreateClient().GetAsync("/metrics")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
