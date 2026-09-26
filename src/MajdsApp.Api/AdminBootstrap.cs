using MajdsApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp;

/// <summary>
/// Creates the first administrator on a brand-new installation, from configuration (<c>Bootstrap:AdminEmail</c> and <c>Bootstrap:AdminPassword</c>; put the
/// password in user secrets or an environment variable, never in a committed file). It only ever acts when there are <b>no users at all</b>, so it cannot
/// be used to bring an administrator back into a running system or to reset a password; once anyone exists it does nothing.
/// </summary>
public static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return;

        var db = services.GetRequiredService<ApplicationDbContext>();
        if (await db.Users.IgnoreQueryFilters().AnyAsync()) return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Administrator", IsActive = true };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            logger.LogError("The first administrator could not be created: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRoleAsync(admin, MajdsApp.Modules.Authorization.PermissionChecker.SuperAdminRoleName);
        logger.LogWarning("Created the first administrator {Email} from configuration. Remove Bootstrap:AdminPassword from configuration now.", email);
    }
}
