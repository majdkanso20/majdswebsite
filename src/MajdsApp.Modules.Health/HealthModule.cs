using System.Text.Json;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MajdsApp.Modules.Health;

/// <summary>
/// F-Health: anonymous liveness/readiness probes for orchestrators and monitors. Like the Identity
/// endpoints these are intentionally outside the ResponseDto envelope — probes are consumed by
/// infrastructure that expects the standard status code (200 healthy / 503 unhealthy), not by the SPA's
/// client. /health/live runs no checks (the process answers); /health/ready runs them all.
/// </summary>
public class HealthModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<FileStorageHealthCheck>("file-storage");
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new() { Predicate = _ => false, ResponseWriter = WriteAsync })
            .AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new() { ResponseWriter = WriteAsync })
            .AllowAnonymous();
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
