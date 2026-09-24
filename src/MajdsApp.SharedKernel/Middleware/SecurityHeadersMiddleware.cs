using Microsoft.AspNetCore.Http;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>
/// Adds baseline response hardening headers to every API response (NFR-SEC-2, FR-XC-005). This host
/// only serves JSON and file downloads, so the CSP is the strictest possible (no scripts, no framing);
/// the Swagger UI, which needs inline scripts and styles, is exempted from the CSP only.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (!context.Request.Path.StartsWithSegments("/swagger"))
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

            return Task.CompletedTask;
        });

        return next(context);
    }
}
