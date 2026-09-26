using System.Security.Claims;
using System.Threading.RateLimiting;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Localization;
using Microsoft.AspNetCore.Builder;
using System.ComponentModel.DataAnnotations;
using MajdsApp.SharedKernel.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.SharedKernel.Security;

/// <summary>Named rate-limit policies an endpoint can opt into with <c>[EnableRateLimiting]</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Sign-in, registration and password recovery: brute-force and email-flood targets, limited per client IP.</summary>
    public const string Auth = "auth";

    /// <summary>Costly reads (exports, global search), limited per user.</summary>
    public const string Expensive = "expensive";
}

/// <summary>
/// Rate limiting (NFR-SEC-4). Every request is subject to a generous global limit per user (or per IP
/// when anonymous); the auth and expensive endpoints get much tighter ones. Limits come from the
/// <c>RateLimiting</c> configuration section so a deployment can tune them without code changes.
/// Rejections use the standard <see cref="ResponseDto{T}"/> envelope with a <c>Retry-After</c> header.
/// </summary>
/// <summary>The <c>RateLimiting</c> section.</summary>
public class RateLimitOptions
{
    [Range(1, 1_000_000, ErrorMessage = "RateLimiting:AuthPermitLimit must be between 1 and 1000000.")]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, 1_000_000, ErrorMessage = "RateLimiting:ExpensivePermitLimit must be between 1 and 1000000.")]
    public int ExpensivePermitLimit { get; set; } = 30;

    [Range(1, 10_000_000, ErrorMessage = "RateLimiting:GlobalPermitLimit must be between 1 and 10000000.")]
    public int GlobalPermitLimit { get; set; } = 600;

    [Range(1, 86_400, ErrorMessage = "RateLimiting:WindowSeconds must be between 1 and 86400.")]
    public int WindowSeconds { get; set; } = 60;
}

public static class RateLimiting
{
    public static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Checked when the application starts (P2 FR-MOD-004): a limit of zero or a negative window would otherwise only show up as every request being refused.
        services.AddModuleOptions<RateLimitOptions>(configuration, "RateLimiting");
        var limits = configuration.GetSection("RateLimiting").Get<RateLimitOptions>() ?? new RateLimitOptions();
        var authLimit = limits.AuthPermitLimit;
        var expensiveLimit = limits.ExpensivePermitLimit;
        var globalLimit = limits.GlobalPermitLimit;
        var window = TimeSpan.FromSeconds(limits.WindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(UserOrIp(context), _ => Fixed(globalLimit, window)));

            options.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter("auth:" + Ip(context), _ => Fixed(authLimit, window)));

            options.AddPolicy(RateLimitPolicies.Expensive, context =>
                RateLimitPartition.GetFixedWindowLimiter("expensive:" + UserOrIp(context), _ => Fixed(expensiveLimit, window)));

            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ResponseDto.Fail<object>(ResponseStatusCode.TooManyRequests, context.HttpContext.Localize("Too many requests. Please wait a moment and try again.")), ct);
            };
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions Fixed(int permits, TimeSpan window) =>
        new() { PermitLimit = permits, Window = window, QueueLimit = 0, AutoReplenishment = true };

    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string UserOrIp(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? "user:" + id : "ip:" + Ip(context);
}
