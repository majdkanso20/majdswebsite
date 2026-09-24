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
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
