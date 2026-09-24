using MajdsApp.Data;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Authorization;

/// <summary>Seeds the baseline roles on first run (FR-ROLE-005). Idempotent — safe to call on every startup.</summary>
public static class RoleSeeder
{
    public const string AdminRoleName = PermissionChecker.SuperAdminRoleName;
    public const string UserRoleName = "User";

    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager)
    {
        if (await roleManager.FindByNameAsync(AdminRoleName) is null)
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Name = AdminRoleName,
                DisplayName = "Administrator",
                IsStatic = true,
                IsDefault = false
            });
        }

        if (await roleManager.FindByNameAsync(UserRoleName) is null)
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Name = UserRoleName,
                DisplayName = "User",
                IsStatic = true,
                IsDefault = true
            });
        }
    }
}
