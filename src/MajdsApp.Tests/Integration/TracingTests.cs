using System.Diagnostics;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace MajdsApp.Tests.Integration;

public class TracingFactory : ApiFactory
{
    public static readonly List<Activity> Spans = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // What a tracing backend would receive, collected in memory.
        builder.ConfigureServices(services => services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(Spans)));
    }
}

/// <summary>F-Health FR-HEALTH-003: requests are traced (HTTP request, the command or query it ran) with the trace id the correlation header and the logs carry, and exported when a backend is set.</summary>
public class TracingTests(TracingFactory factory) : IClassFixture<TracingFactory>
{
    private static async Task<List<Activity>> SpansOfAsync(string traceId)
    {
        for (var i = 0; i < 20; i++)
        {
            lock (TracingFactory.Spans)
            {
                var found = TracingFactory.Spans.Where(s => s.TraceId.ToString() == traceId).ToList();
                if (found.Any(s => s.DisplayName == "GetMySettingsQuery")) return found;
            }
            await Task.Delay(100);
        }

        lock (TracingFactory.Spans) return TracingFactory.Spans.Where(s => s.TraceId.ToString() == traceId).ToList();
    }

    [Fact]
    public async Task A_request_produces_an_http_span_and_a_span_for_the_query_it_ran_under_one_trace_id_that_the_response_header_carries()
    {
        var admin = await factory.SignInAsync("tr.admin1@example.com", "Admin");

        var response = await admin.Http.GetAsync("/api/settings/my");
        var traceId = response.Headers.GetValues("X-Correlation-Id").Single();
        var spans = await SpansOfAsync(traceId);

        var http = spans.Should().Contain(s => s.Kind == ActivityKind.Server).Subject;
        var query = spans.Should().ContainSingle(s => s.DisplayName == "GetMySettingsQuery").Subject;
        query.ParentSpanId.Should().Be(http.SpanId, "the query runs inside the request");
    }

    [Fact]
    public async Task A_trace_started_by_an_upstream_service_is_continued()
    {
        var admin = await factory.SignInAsync("tr.admin2@example.com", "Admin");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/settings/my");
        request.Headers.Add("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        var response = await admin.Http.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be("0af7651916cd43dd8448eb211c80319c");
        var spans = await SpansOfAsync("0af7651916cd43dd8448eb211c80319c");
        spans.Should().Contain(s => s.Kind == ActivityKind.Server && s.ParentSpanId.ToString() == "b7ad6b7169203331");
    }

    [Fact]
    public async Task The_health_probes_are_not_traced()
    {
        var response = await factory.Anonymous().Http.GetAsync("/health/live");
        var traceId = response.Headers.GetValues("X-Correlation-Id").Single();
        await Task.Delay(500);

        lock (TracingFactory.Spans) TracingFactory.Spans.Should().NotContain(s => s.TraceId.ToString() == traceId);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://collector")]
    public void A_backend_address_that_is_not_a_web_address_stops_the_application_from_starting(string endpoint)
    {
        using var bad = new ApiFactory(new Dictionary<string, string> { ["Telemetry:OtlpEndpoint"] = endpoint }, null);

        bad.Invoking(f => f.CreateClient()).Should().Throw<Exception>().Which.ToString().Should().Contain("Telemetry:OtlpEndpoint");
    }

    [Fact]
    public void A_backend_address_is_accepted()
    {
        using var ok = new ApiFactory(new Dictionary<string, string> { ["Telemetry:OtlpEndpoint"] = "http://localhost:4317" }, null);

        ok.Invoking(f => f.CreateClient()).Should().NotThrow();
    }
}
