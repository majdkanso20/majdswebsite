using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MajdsApp.Modules.ApiDocs;

/// <summary>The <c>Docs</c> section: whether the API docs are on outside Development and who may read them.</summary>
public class DocsOptions
{
    public bool Enabled { get; set; }

    [RegularExpression("^(?i:Permission|Authenticated|Open)$", ErrorMessage = "Docs:Access must be Permission, Authenticated or Open.")]
    public string Access { get; set; } = "Permission";
}

public static class Permissions
{
    public static class Docs
    {
        public const string View = "Docs.View";
    }
}

/// <summary>API documentation and versioning (F-ApiDocs). The wiring lives in <see cref="ApiDocsExtensions"/>, which the host calls at startup.</summary>
public class ApiDocsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // The versioning and Swagger wiring needs the MVC builder, so the host calls AddPlatformApiDocs after the controllers are added.
        // The module's own settings are bound and checked at start here (P2 FR-MOD-004).
        services.AddModuleOptions<DocsOptions>(configuration, "Docs");
    }
}

public static class ApiDocsExtensions
{
    /// <summary>
    /// API versioning and one OpenAPI document per version (FR-DOC-001/003). A request names its version with <c>?api-version=2.0</c> or the
    /// <c>X-Api-Version</c> header; without one it gets the default, 1.0, so every existing route keeps working unchanged. Responses list the
    /// versions the endpoint supports in <c>api-supported-versions</c>. To add a version, mark a controller <c>[ApiVersion("2.0")]</c>: it appears in
    /// its own document at <c>/swagger/v2/swagger.json</c> with no other change.
    /// </summary>
    public static IServiceCollection AddPlatformApiDocs(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(new QueryStringApiVersionReader("api-version"), new HeaderApiVersionReader("X-Api-Version"));
        }).AddMvc().AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = false;
        });

        services.AddSwaggerGen();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwagger>();
        return services;
    }

    /// <summary>
    /// The documentation endpoints (FR-DOC-004). In Development they are open. Elsewhere they are off unless <c>Docs:Enabled</c> is <c>true</c>, and then
    /// <c>Docs:Access</c> decides who may read them: <c>Permission</c> (the default: a signed-in user holding <c>Docs.View</c>), <c>Authenticated</c>
    /// (any signed-in user) or <c>Open</c>. Call it after authentication.
    /// </summary>
    public static WebApplication UsePlatformApiDocs(this WebApplication app)
    {
        app.UseMiddleware<DocsAccessMiddleware>();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in app.DescribeApiVersions())
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"Majd's App API {description.GroupName}");
            options.ConfigObject.PersistAuthorization = true; // the token entered under Authorize survives a page reload
        });
        return app;
    }

    private sealed class ConfigureSwagger(IApiVersionDescriptionProvider versions) : IConfigureOptions<SwaggerGenOptions>
    {
        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in versions.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, new OpenApiInfo
                {
                    Title = "Majd's App API",
                    Version = description.ApiVersion.ToString(),
                    Description = description.IsDeprecated ? "This version of the API is deprecated." : null
                });
            }

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token returned by POST /api/identity/login (no 'Bearer ' prefix needed here)."
            });

            // Without a requirement the scheme is only declared: the Authorize button would show but "Try it out" would not send the token (AC-DOC-2).
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }
    }
}

/// <summary>Guards <c>/swagger</c> (the interactive UI and the OpenAPI documents) according to environment and configuration; see <see cref="ApiDocsExtensions.UsePlatformApiDocs"/>.</summary>
public class DocsAccessMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/swagger") || environment.IsDevelopment())
        {
            await next(context);
            return;
        }

        if (configuration["Docs:Enabled"] is not "true")
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var access = configuration["Docs:Access"] is { Length: > 0 } configured ? configured : "Permission";
        if (access.Equals("Open", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        if (access.Equals("Permission", StringComparison.OrdinalIgnoreCase)
            && !await context.RequestServices.GetRequiredService<IPermissionChecker>().HasPermissionAsync(Permissions.Docs.View, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
