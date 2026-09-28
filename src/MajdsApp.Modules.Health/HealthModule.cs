using System.Net.Sockets;
using System.Text.Json;
using MajdsApp.Configuration;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Prometheus;

namespace MajdsApp.Modules.Health;

/// <summary>
/// F-Health: anonymous liveness/readiness probes for orchestrators and monitors, and request metrics for a scraper. Like the Identity
/// endpoints these are intentionally outside the ResponseDto envelope: probes are consumed by infrastructure that expects the standard status
/// code (200 healthy / 503 unhealthy), not by the SPA's client. /health/live runs no checks (the process answers); /health/ready runs them all.
/// A critical dependency that is down (the database, file storage) makes readiness unhealthy (503); one the platform can work without (the cache,
/// which falls back to the database, and email, which is queued and retried) only degrades it (still 200, reported as Degraded) (FR-HEALTH-004).
/// </summary>
public class HealthModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleOptions<MetricsOptions>(configuration, "Metrics"); // bound and checked at start (P2 FR-MOD-004)
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy)
            .AddCheck<FileStorageHealthCheck>("file-storage", HealthStatus.Unhealthy)
            .AddCheck<CacheHealthCheck>("cache", HealthStatus.Degraded)
            .AddCheck<EmailHealthCheck>("email", HealthStatus.Degraded);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new() { Predicate = _ => false, ResponseWriter = WriteAsync })
            .AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new() { ResponseWriter = WriteAsync })
            .AllowAnonymous();

        // Request rate, latency and error rate (by status code) in the Prometheus format (FR-HEALTH-002). Who may read it is decided by MetricsAccessMiddleware.
        endpoints.MapMetrics("/metrics").AllowAnonymous();
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                ms = (int)e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception is null ? null : "Check failed."
            })
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}

public class DatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the database.");
}

public class FileStorageHealthCheck(IWebHostEnvironment env) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.Combine(env.ContentRootPath, "App_Data", "files");
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("File storage is not writable.", ex));
        }
    }
}

/// <summary>Writes, reads and removes a value through the platform cache, so a Redis that is down or misconfigured shows up here. The cache is an
/// optimization (a miss falls back to the database), so a failure is Degraded, not Unhealthy.</summary>
public class CacheHealthCheck(ICacheService cache, CacheOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var key = "health:" + Guid.NewGuid().ToString("N");
        var value = Guid.NewGuid().ToString("N");

        await cache.SetAsync(key, value, TimeSpan.FromSeconds(30), ct);
        var read = await cache.GetAsync<string>(key, ct);
        await cache.RemoveAsync(key, ct);

        return read == value
            ? HealthCheckResult.Healthy($"{options.Provider} cache is working.")
            : HealthCheckResult.Degraded($"The {options.Provider} cache did not return what was stored; reads fall back to the database.");
    }
}

/// <summary>Opens a connection to the configured mail server (nothing is sent). Skipped, and reported as such, when email is not configured. Mail is
/// queued and retried, so an unreachable server is Degraded.</summary>
public class EmailHealthCheck(IServiceProvider services, IOptions<SmtpOptions> smtp, ISettingsProvider settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (services.GetService<IEmailMessageSender>() is null)
            return HealthCheckResult.Healthy("Email is not configured.");

        var host = await settings.GetAsync("Email.SmtpHost", ct);
        if (string.IsNullOrWhiteSpace(host)) host = smtp.Value.Host;
        var port = await settings.GetIntegerAsync("Email.SmtpPort", ct);
        if (port <= 0) port = smtp.Value.Port;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token);
            return HealthCheckResult.Healthy("The mail server accepts connections.");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            return HealthCheckResult.Degraded("The mail server cannot be reached; email is queued and retried.");
        }
    }
}

/// <summary>
/// Decides who may read <c>/metrics</c>. Metrics describe traffic and are not public data: with <c>Metrics:Token</c> set, a scraper must send it as a
/// bearer token; with no token the endpoint is open only in Development (or when <c>Metrics:AllowAnonymous</c> is <c>true</c>) and otherwise not there at all.
/// <c>Metrics:Enabled=false</c> turns it off.
/// </summary>
/// <summary>The <c>Metrics</c> section: whether <c>/metrics</c> is served and who may read it (see the Health README).</summary>
public class MetricsOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>When set, a scraper must send <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public string? Token { get; set; }

    /// <summary>Lets anyone read the metrics outside Development when there is no token.</summary>
    public bool AllowAnonymous { get; set; }
}

public class MetricsAccessMiddleware(RequestDelegate next, Microsoft.Extensions.Options.IOptions<MetricsOptions> options, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/metrics"))
        {
            await next(context);
            return;
        }

        var token = options.Value.Token;
        var enabled = options.Value.Enabled;
        var open = environment.IsDevelopment() || options.Value.AllowAnonymous;

        if (!enabled || (string.IsNullOrEmpty(token) && !open))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!string.IsNullOrEmpty(token))
        {
            var supplied = context.Request.Headers.Authorization.ToString();
            var expected = "Bearer " + token;
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes(expected)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return;
            }
        }

        await next(context);
    }
}

public static class HealthApplicationExtensions
{
    /// <summary>Records request count, latency and status code for every request, and guards <c>/metrics</c>. Call it early, so the timings cover the rest of the pipeline.</summary>
    public static IApplicationBuilder UsePlatformMetrics(this IApplicationBuilder app)
    {
        app.UseMiddleware<MetricsAccessMiddleware>();
        return app.UseHttpMetrics();
    }
}
