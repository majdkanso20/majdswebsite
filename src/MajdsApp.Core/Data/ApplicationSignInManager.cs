using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Data;

/// <summary>
/// Honors <see cref="ApplicationUser.IsActive"/> at sign-in time (F-Users FR-USER-004, AC-USER-2) —
/// a deactivated user is denied authentication on every surface (cookie login, bearer login,
/// external login) since they all route through <see cref="CanSignInAsync"/>.
/// </summary>
public class ApplicationSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    Microsoft.Extensions.Options.IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.IsActive && await base.CanSignInAsync(user);
}
