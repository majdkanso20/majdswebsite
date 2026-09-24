using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>Propagates/generates a correlation id per request and enriches structured logs with it
/// plus the current user id (FR-ERR-003, NFR-OBS-1).</summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("n");

        context.Response.Headers[HeaderName] = correlationId;
        context.Items[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null))
        {
            await next(context);
        }
    }
}
