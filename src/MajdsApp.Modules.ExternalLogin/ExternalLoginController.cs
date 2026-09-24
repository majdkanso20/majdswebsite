using MajdsApp.Data;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MajdsApp.Modules.ExternalLogin;

public class ExternalLoginModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Controller only; the Google/Microsoft handlers and the external cookie are host wiring (Program.cs).
    }
}

/// <summary>
/// "Sign in with Google/Microsoft" for the SPA. The SPA can't take part in the OAuth redirect dance
/// itself, so the browser is sent to <c>/start</c>, the provider calls back to the middleware
/// (<c>/signin-google</c>), that lands on <c>/callback</c> here, and we hand the SPA a normal Identity
/// bearer token in the URL *fragment* (never sent to a server or logged). Same users, roles and
/// passwords as everywhere else — this only adds another way to prove who someone is.
/// These endpoints are browser navigations, so they redirect rather than return the ResponseDto envelope.
/// </summary>
[Route("api/external")]
public class ExternalLoginController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IAuthenticationSchemeProvider schemes,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    ISettingsProvider settings,
    IConfiguration configuration,
    TimeProvider clock,
    IUserNotificationPublisher notifications,
    ILogger<ExternalLoginController> logger) : ApiControllerBase
{
    private static readonly Dictionary<string, string> ProviderSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google"] = "Google",
        ["microsoft"] = "MicrosoftAccount"
    };

    /// <summary>Which providers have credentials configured, so the login page only shows working buttons.</summary>
    [AllowAnonymous]
    [HttpGet("providers")]
    public async Task<ResponseDto<string[]>> Providers()
    {
        var registered = (await schemes.GetAllSchemesAsync()).Select(s => s.Name).ToHashSet();
        return Ok(ProviderSchemes.Where(p => registered.Contains(p.Value)).Select(p => p.Key).ToArray());
    }

    [AllowAnonymous]
    [HttpGet("{provider}/start")]
    public IActionResult Start(string provider, [FromQuery] string returnUrl)
    {
        if (!IsAllowedReturnUrl(returnUrl) || !ProviderSchemes.TryGetValue(provider, out var scheme))
            return BadRequest("Invalid request.");

        var callback = Url.Action(nameof(Callback), new { returnUrl })!;
        return Challenge(signInManager.ConfigureExternalAuthenticationProperties(scheme, callback), scheme);
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string returnUrl)
    {
        if (!IsAllowedReturnUrl(returnUrl))
            return BadRequest("Invalid request.");

        try
        {
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null)
                return Fail(returnUrl, "external_failed");

            var user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user is null)
            {
                var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (string.IsNullOrWhiteSpace(email))
                    return Fail(returnUrl, "no_email");

                user = await userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    if (!await settings.GetBooleanAsync("Security.AllowSelfRegistration"))
                        return Fail(returnUrl, "registration_disabled");

                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true, // the provider has verified this address
                        FullName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                    };

                    var created = await userManager.CreateAsync(user);
                    if (!created.Succeeded)
                        return Fail(returnUrl, "external_failed");

                    var defaultRoles = await roleManager.Roles.Where(r => r.IsDefault).Select(r => r.Name!).ToListAsync();
                    if (defaultRoles.Count > 0)
                        await userManager.AddToRolesAsync(user, defaultRoles);

                    await notifications.PublishToRoleAsync("Admin", "New user signed up", $"{email} joined with {info.LoginProvider}.", NotificationTypes.Administration);
                }

                var linked = await userManager.AddLoginAsync(user, info);
                if (!linked.Succeeded)
                    return Fail(returnUrl, "external_failed");
            }

            if (!user.IsActive || await userManager.IsLockedOutAsync(user))
                return Fail(returnUrl, "not_allowed");

            // Deliberately no app-level 2FA prompt here: the provider (Google) has already authenticated
            // the person with its own sign-in and any 2-step verification on that account. This differs
            // from Identity's default external-login behavior, which re-asks for the app's second factor.

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            var options = bearerOptions.Get(IdentityConstants.BearerScheme);
            var properties = new AuthenticationProperties { IssuedUtc = clock.GetUtcNow(), ExpiresUtc = clock.GetUtcNow().Add(options.BearerTokenExpiration) };
            var token = options.BearerTokenProtector.Protect(new AuthenticationTicket(principal, properties, IdentityConstants.BearerScheme));

            return Redirect($"{returnUrl}#access_token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email ?? string.Empty)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "External login callback failed");
            return Fail(returnUrl, "external_failed");
        }
        finally
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }
    }

    private RedirectResult Fail(string returnUrl, string code) => Redirect($"{returnUrl}#error={code}");

    /// <summary>The token goes to returnUrl, so it must be one of the configured SPA origins — an open
    /// redirect here would hand a valid token to any site.</summary>
    private bool IsAllowedReturnUrl(string? returnUrl)
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        var allowed = configuration.GetSection("Spa:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
        return allowed.Any(o => string.Equals(o.TrimEnd('/'), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
    }
}
