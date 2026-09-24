using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

/// <summary>FR-USER-007: an admin can't delete/deactivate their own account here, and the last
/// remaining Admin can never be removed or deactivated by anyone.</summary>
public class SuperAdminGuard(UserManager<ApplicationUser> userManager, ICurrentUser currentUser) : IScopedService
{
    public void EnsureNotSelf(string targetUserId)
    {
        if (targetUserId == currentUser.UserId)
            throw new ConflictException("You cannot delete or deactivate your own account from here.");
    }

    public async Task EnsureNotLastSuperAdminAsync(ApplicationUser targetUser, CancellationToken ct)
    {
        if (!await userManager.IsInRoleAsync(targetUser, RoleSeeder.AdminRoleName))
            return;

        var admins = await userManager.GetUsersInRoleAsync(RoleSeeder.AdminRoleName);
        if (admins.Count(a => a.IsActive && !a.IsDeleted) <= 1)
            throw new ConflictException("At least one active administrator must remain.");
    }
}
