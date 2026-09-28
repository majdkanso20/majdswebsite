using MajdsApp.SharedKernel.Middleware;
using Microsoft.AspNetCore.Builder;

namespace MajdsApp.SharedKernel;

public static class PlatformApplicationBuilderExtensions
{
    /// <summary>
    /// Wires the mandatory request pipeline (correlation id -> exception handling) ahead of
    /// authentication/authorization/MVC (P1 §2.3). Call this first in the middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UsePlatformCore(this IApplicationBuilder app)
    {
        // Outermost: sees the final status code of everything downstream, including a short-circuit
        // (deny-by-default, an unmatched route) that never reaches the exception handler or a controller (FR-XC-005).
        app.UseMiddleware<ResponseWrappingMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
