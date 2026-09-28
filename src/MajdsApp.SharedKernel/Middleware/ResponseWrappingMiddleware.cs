using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Localization;
using Microsoft.AspNetCore.Http;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>
/// Wraps the API's own short-circuits — the deny-by-default fallback authorization policy refusing a
/// request that matches no <c>[Authorize]</c>/<c>[AllowAnonymous]</c> endpoint or no endpoint at all (401),
/// and a path nothing maps to (404) — in the same <see cref="ResponseDto{T}"/> envelope a handled request
/// gets (FR-XC-005). Everything else already writes its own body: <see cref="ExceptionHandlingMiddleware"/>
/// for a thrown exception, the rate limiter's own <c>OnRejected</c> for 429, and MVC's <see cref="ResponseStatusCodeFilter"/>
/// and controllers for anything a handler ran and returned. This is the leftover case: nothing downstream wrote
/// anything at all, so this is the last chance to make the response look like the rest of the API.
/// </summary>
public class ResponseWrappingMiddleware(RequestDelegate next)
{
    private static readonly Dictionary<int, ResponseStatusCode> Mapped = new()
    {
        [StatusCodes.Status400BadRequest] = ResponseStatusCode.ValidationError,
        [StatusCodes.Status401Unauthorized] = ResponseStatusCode.Unauthorized,
        [StatusCodes.Status403Forbidden] = ResponseStatusCode.Forbidden,
        [StatusCodes.Status404NotFound] = ResponseStatusCode.NotFound,
        [StatusCodes.Status409Conflict] = ResponseStatusCode.Conflict,
        [StatusCodes.Status429TooManyRequests] = ResponseStatusCode.TooManyRequests
    };

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // Not our concern outside the API: a missing static asset, an unknown SignalR or Swagger path, and the
        // like should stay exactly what ASP.NET would otherwise send (RefusedRequestAuditMiddleware scopes the
        // same way). And if anything downstream already wrote a body, leave it alone.
        if (!context.Request.Path.StartsWithSegments("/api")) return;
        if (context.Response.HasStarted || context.Response.ContentLength.GetValueOrDefault() > 0 || !string.IsNullOrEmpty(context.Response.ContentType)) return;
        if (!Mapped.TryGetValue(context.Response.StatusCode, out var code)) return;

        context.Response.ContentType = "application/json";
        var response = ResponseDto.Fail<object>(code, context.Localize(code.GetDescription() ?? string.Empty));
        await context.Response.WriteAsJsonAsync(response);
    }
}
