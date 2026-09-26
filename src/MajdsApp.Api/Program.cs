using MajdsApp.Configuration;
using MajdsApp.Data;
using MajdsApp.Modules.ApiDocs;
using MajdsApp.Modules.Health;
using MajdsApp.Modules.Localization;
using MajdsApp.Services;
using MajdsApp.SharedKernel;
using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
MajdsApp.LoggingSetup.AddPlatformLogging(builder);
builder.Services.AddModuleOptions<MajdsApp.LogSinkOptions>(builder.Configuration, "Logging"); // bound and checked at start

// Every feature module project referenced by this host — the single place a new module is "wired in"
// by adding one line here, per P2 (no other host code changes per feature).
var moduleAssemblies = new[]
{
    typeof(MajdsApp.Modules.Diagnostics.DiagnosticsModule).Assembly,
    typeof(MajdsApp.Modules.Authorization.AuthorizationModule).Assembly,
    typeof(MajdsApp.Modules.Roles.RolesModule).Assembly,
    typeof(MajdsApp.Modules.Users.UsersModule).Assembly,
    typeof(MajdsApp.Modules.Settings.SettingsModule).Assembly,
    typeof(MajdsApp.Modules.Audit.AuditModule).Assembly,
    typeof(MajdsApp.Modules.Notifications.NotificationsModule).Assembly,
    typeof(MajdsApp.Modules.Files.FilesModule).Assembly,
    typeof(MajdsApp.Modules.Account.AccountModule).Assembly,
    typeof(MajdsApp.Modules.Health.HealthModule).Assembly,
    typeof(MajdsApp.Modules.Jobs.JobsModule).Assembly,
    typeof(MajdsApp.Modules.Features.FeaturesModule).Assembly,
    typeof(MajdsApp.Modules.Search.SearchModule).Assembly,
    typeof(MajdsApp.Modules.ExternalLogin.ExternalLoginModule).Assembly,
    typeof(MajdsApp.Modules.Plugins.PluginsModule).Assembly,
    typeof(MajdsApp.Modules.Dashboard.DashboardModule).Assembly,
    typeof(MajdsApp.Modules.Exports.ExportsModule).Assembly,
    typeof(MajdsApp.Modules.Localization.LocalizationModule).Assembly,
    typeof(MajdsApp.Modules.ApiDocs.ApiDocsModule).Assembly
};

// P5: runtime-deployable plugins. Each subfolder of the plugins directory with a plugin.json +
// backend/<assembly> is loaded into its own isolated AssemblyLoadContext (FR-PLUG-005/006) — this
// must happen before AddPlatformCore/AddDbContext so the plugin's MediatR handlers, controllers, and
// EF entity configs are discovered by the exact same reflection those already scan (no parallel
// mechanism). A plugin that fails to load is recorded with its error and skipped, never crashes the
// host (FR-PLUG-008).
var pluginsDirectory = builder.Configuration["Plugins:Directory"]
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "plugins"));
var loadedPlugins = PluginManager.LoadAll(pluginsDirectory);
var pluginAssemblies = loadedPlugins.Where(p => p.Succeeded).Select(p => p.Assembly!).ToArray();
builder.Services.AddSingleton(loadedPlugins);

// Where plugins live and who may install one (P5 FR-PLUG-036). With Plugins:Trust:RequireAllowList on, only packages
// listed under Plugins:Trust:Allowed (id + SHA-256 of the package file) can be installed through the admin screen.
builder.Services.AddSingleton(PluginHostOptions.FromConfiguration(builder.Configuration, pluginsDirectory));

var allModuleAssemblies = moduleAssemblies.Concat(pluginAssemblies).ToArray();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
        .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork<ApplicationDbContext>>();
// Repositories can be injected directly; every one in a request shares the request's DbContext (P3 FR-REPO-004/006).
builder.Services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
builder.Services.AddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>));

// Bridges the existing Identity system (AS-4) to bearer tokens for the Angular SPA — same user
// store, same passwords/2FA/external logins already built; this just adds a token-issuing surface
// alongside MajdsApp's cookie-based one. Not a new auth system, so it's exempt from the ResponseDto
// envelope/GET-POST convention, same as F-Health's probes.
builder.Services
    .AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()
    .AddPasswordValidator<MinimumLengthPasswordValidator>() // the administrator's minimum length, added to Identity's own rules (FR-USER-008)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints()
    .AddSignInManager<ApplicationSignInManager>(); // Must come after AddApiEndpoints() — it registers its own
                                                    // SignInManager<TUser>, and plain AddScoped means last wins.

var authentication = builder.Services.AddAuthentication(IdentityConstants.BearerScheme);
// The token's own lifetime is just a ceiling (24h); the effective session length is the
// Security.DefaultUserSessionTimeoutMinutes setting, enforced per request by the middleware below.
var bearerTokenLifetime = TimeSpan.FromDays(1);
authentication.AddBearerToken(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = bearerTokenLifetime;
    // Browsers cannot set headers on a WebSocket, so the SignalR client sends the token in the query string.
    options.Events.OnMessageReceived = context =>
    {
        var token = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            context.Token = token;
        return Task.CompletedTask;
    };
});

// External logins (F-Account, Google/Microsoft): same opt-in-by-configuration rule as the Razor site —
// a provider is only registered when its credentials exist (shared user-secrets, see the csproj). The
// OAuth handshake needs a short-lived cookie to carry the result from /signin-google to our callback.
authentication.AddCookie(IdentityConstants.ExternalScheme, options =>
{
    options.Cookie.Name = IdentityConstants.ExternalScheme;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authentication.AddGoogle(options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
{
    authentication.AddMicrosoftAccount(options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
    });
}
// Deny by default (NFR-SEC-1): any endpoint without its own [Authorize]/[AllowAnonymous] decision
// requires a signed-in user, so a forgotten attribute can never leave something open.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build());
MajdsApp.SharedKernel.Security.RateLimiting.AddPlatformRateLimiting(builder.Services, builder.Configuration);

// Same conditional SMTP wiring as MajdsApp's Program.cs (see that file for the full rationale) —
// needed here too since F-Users' admin-initiated password reset (FR-USER-006) sends real email.
builder.Services.AddModuleOptions<SmtpOptions>(builder.Configuration, "Email:Smtp"); // bound and checked at start (P2 FR-MOD-004)
var smtpUsername = builder.Configuration["Email:Smtp:Username"];
var smtpPassword = builder.Configuration["Email:Smtp:Password"];
if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
{
    builder.Services.AddTransient<IEmailSender<ApplicationUser>, SmtpEmailSender>();
    builder.Services.AddTransient<MajdsApp.SharedKernel.Notifications.IEmailMessageSender, SmtpEmailSender>();
    // Retry, a timeout and a circuit breaker around sending, added by decorating the sender rather than editing it (P4 FR-XC-004/006).
    builder.Services.AddModuleOptions<MajdsApp.SharedKernel.Resilience.OutboundResilienceOptions>(builder.Configuration, "Resilience");
    builder.Services.AddSingleton<EmailPipeline>();
    builder.Services.Decorate<MajdsApp.SharedKernel.Notifications.IEmailMessageSender, ResilientEmailMessageSender>();
}

builder.Services.AddPlatformCore(builder.Configuration, allModuleAssemblies);
builder.Services.AddPlatformControllers(allModuleAssemblies);

// The SPA's origin(s) come from configuration (Spa:AllowedOrigins) in every environment — the same
// list the external-login redirect uses — so a real deployment just sets its own address instead of
// editing code (NFR-SEC-2). Defaults to the Angular dev server.
const string SpaCorsPolicy = "Spa";
var spaOrigins = builder.Configuration.GetSection("Spa:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(SpaCorsPolicy, policy =>
        policy.WithOrigins(spaOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddPlatformApiDocs(); // API versioning and the OpenAPI documents (F-ApiDocs)

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await MajdsApp.Modules.Authorization.RoleSeeder.SeedAsync(roleManager);

    var pluginDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pluginCache = scope.ServiceProvider.GetRequiredService<MajdsApp.Modules.Plugins.PluginStateCache>();
    await MajdsApp.Modules.Plugins.PluginRegistrySync.SyncAsync(pluginDb, loadedPlugins, pluginCache, app.Services, app.Logger);
}

// Metrics wrap everything, including the exception handler, so an error the handler turns into a 403 or 500 is counted with its real status (F-Health).
app.UsePlatformMetrics();
app.UsePlatformCore();

if (!app.Environment.IsDevelopment())
    app.UseHsts(); // NFR-SEC-2 (browsers ignore HSTS on localhost, so it is production-only)

app.UseHttpsRedirection();

app.UseCors(SpaCorsPolicy);

app.UseAuthentication();
app.UsePlatformApiDocs(); // after authentication: outside Development the docs need a signed-in user with Docs.View (F-ApiDocs)
app.UsePlatformLocalization(); // after authentication: a signed-in user's saved language decides the response language when the client does not ask for one

// Enforces the session-timeout setting: a sign-in older than the configured number of minutes is
// rejected with 401, which the SPA turns into a redirect to the login page.
app.Use(async (context, next) =>
{
    // Bearer tickets carry only an expiry, so the sign-in time is expiry minus the token lifetime.
    var properties = context.Features.Get<Microsoft.AspNetCore.Authentication.IAuthenticateResultFeature>()
        ?.AuthenticateResult?.Properties;
    var issuedUtc = properties?.IssuedUtc ?? (properties?.ExpiresUtc - bearerTokenLifetime);

    if (context.User.Identity?.IsAuthenticated == true && issuedUtc is not null)
    {
        var settings = context.RequestServices.GetRequiredService<MajdsApp.SharedKernel.Settings.ISettingsProvider>();
        var minutes = Math.Clamp(await settings.GetIntegerAsync("Security.DefaultUserSessionTimeoutMinutes"), 1, 1440);
        if (DateTimeOffset.UtcNow - issuedUtc.Value > TimeSpan.FromMinutes(minutes))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

app.UseRateLimiter(); // after authentication so the global limit can key on the signed-in user

app.UseAuthorization();

// The Identity endpoints (login, register, refresh, password reset, ...) carry no [AllowAnonymous] of
// their own, so under the deny-by-default policy they must be opened explicitly. Everything under
// /manage stays authenticated: it declares RequireAuthorization itself, and AllowAnonymous would override it.
app.MapGroup("/api/identity")
    .RequireRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Auth)
    .MapIdentityApi<ApplicationUser>()
    .Add(endpoint =>
    {
        if (endpoint is Microsoft.AspNetCore.Routing.RouteEndpointBuilder route
            && route.RoutePattern.RawText?.Contains("/manage", StringComparison.OrdinalIgnoreCase) != true)
            endpoint.Metadata.Add(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());
    });
app.MapControllers();
app.MapFeatureModuleEndpoints(allModuleAssemblies);

app.Run();

// Exposes the entry point so the integration tests can host the real application (WebApplicationFactory).
public partial class Program;
