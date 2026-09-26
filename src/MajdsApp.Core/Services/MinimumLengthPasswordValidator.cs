using MajdsApp.Data;
using MajdsApp.SharedKernel.Settings;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Services;

/// <summary>
/// Enforces the administrator's minimum password length (<c>Security.MinPasswordLength</c>, F-Users FR-USER-008) wherever a password is set (creating a user,
/// registering, changing, resetting), because it is an Identity validator rather than a check in each command. Identity's own rules (at least 6 characters and
/// its character classes) still apply; this can only make the length stricter.
/// </summary>
public class MinimumLengthPasswordValidator(ISettingsProvider settings) : IPasswordValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var minimum = await settings.GetIntegerAsync("Security.MinPasswordLength");
        if (minimum > 0 && (password?.Length ?? 0) < minimum)
            return IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = $"Passwords must be at least {minimum} characters." });

        return IdentityResult.Success;
    }
}
