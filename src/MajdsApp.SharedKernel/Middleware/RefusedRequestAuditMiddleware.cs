using System.Diagnostics;
using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>
/// Records API requests that ASP.NET refused before they reached a handler (F-Audit FR-AUDIT-006): no token or an invalid one (401), a token without the
/// right to the endpoint (403), and a request over the rate limit (429). Refusals made inside a handler are already recorded by the pipeline, and are skipped here
/// (the pipeline marks the request). Because anyone can send an unauthenticated request, the same caller refused on the same address for the same reason is recorded
/// once a minute, so a scanner cannot fill the audit trail.
/// </summary>
public class RefusedRequestAuditMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<RefusedRequestAuditMiddleware> logger)
{
    /// <summary>Set on the request by <c>FailureAuditBehavior</c> when it has already recorded the refusal.</summary>
    public const string AlreadyRecordedKey = "audit.refusal.recorded";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public async Task InvokeAsync(HttpContext context)
    {
        var timer = Stopwatch.StartNew();
        await next(context);

        var status = context.Response.StatusCode;
        if (status is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status429TooManyRequests)) return;
        if (!context.Request.Path.StartsWithSegments("/api") || context.Items.ContainsKey(AlreadyRecordedKey)) return;

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (cache.TryGetValue((ip, context.Request.Path.Value, status), out _)) return;
        cache.Set((ip, context.Request.Path.Value, status), true, Window);

        try
        {
            var user = context.RequestServices.GetRequiredService<ICurrentUser>();
            var (outcome, message) = status switch
            {
                StatusCodes.Status401Unauthorized => ("Unauthorized", "No valid sign-in was presented."),
                StatusCodes.Status403Forbidden => ("Forbidden", "The signed-in user may not use this endpoint."),
                _ => ("RateLimited", "Too many requests.")
            };

            var record = new AuditRecord(
                $"{context.Request.Method} {context.Request.Path.Value}", user.UserId, user.UserName, context.Request.Method, context.Request.Path.Value,
                (int)timer.ElapsedMilliseconds, ip, context.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent[..Math.Min(agent.Length, 300)] : null,
                Succeeded: false, outcome, message, Parameters: null, Changes: []);
            await context.RequestServices.GetRequiredService<IAuditLogWriter>().WriteFailureAsync(record, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not record the refused request to {Path}", context.Request.Path.Value);
        }
    }
}
