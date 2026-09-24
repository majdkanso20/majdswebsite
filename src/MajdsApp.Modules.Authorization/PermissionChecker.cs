using MajdsApp.Data;
using MajdsApp.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MajdsApp.Modules.Authorization;

/// <summary>
/// Real F-Authorization permission check, replacing SharedKernel's permissive default
/// (FR-AUTHZ-003/006/007). Effective permissions = union of granted role permissions, plus direct
/// user grants, minus direct user denies — except the seeded "Admin" role, which implicitly holds
/// every permission and can never be locked out (FR-AUTHZ-007).
/// </summary>
public class PermissionChecker(ApplicationDbContext db, ICurrentUser currentUser, IMemoryCache cache) : IPermissionChecker
{
    public const string SuperAdminRoleName = "Admin";

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return false;

        var effective = await cache.GetOrCreateAsync(CacheKey(currentUser.UserId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await ComputeEffectivePermissionsAsync(currentUser.UserId, ct);
        });

        return effective is not null && (effective.IsSuperAdmin || effective.Permissions.Contains(permission));
    }

    private async Task<EffectivePermissions> ComputeEffectivePermissionsAsync(string userId, CancellationToken ct)
    {
        var roleIds = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct);

        var isSuperAdmin = await db.Roles
            .Where(r => roleIds.Contains(r.Id) && r.Name == SuperAdminRoleName)
            .AnyAsync(ct);

        if (isSuperAdmin)
            return new EffectivePermissions(true, new HashSet<string>());

        var grantedByRole = await db.Set<RolePermission>()
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.IsGranted)
            .Select(rp => rp.PermissionName)
            .ToListAsync(ct);

        var userOverrides = await db.Set<UserPermission>()
            .Where(up => up.UserId == userId)
            .ToListAsync(ct);

        var effective = new HashSet<string>(grantedByRole);
        effective.UnionWith(userOverrides.Where(o => o.IsGranted).Select(o => o.PermissionName));
        effective.ExceptWith(userOverrides.Where(o => !o.IsGranted).Select(o => o.PermissionName));

        return new EffectivePermissions(false, effective);
    }

    /// <summary>Backs GET /api/session/permissions (FR-AUTHZ-005) — the frontend uses this to drive UI visibility.</summary>
    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return [];

        var effective = await cache.GetOrCreateAsync(CacheKey(currentUser.UserId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await ComputeEffectivePermissionsAsync(currentUser.UserId, ct);
        });

        if (effective is null)
            return [];

        return effective.IsSuperAdmin ? PermissionRegistry.GetAllPermissionNames() : effective.Permissions.ToList();
    }

    /// <summary>Called by F-Roles/F-Users after any change to roles/permissions (AC-AUTHZ-2).</summary>
    public void InvalidateUser(string userId) => cache.Remove(CacheKey(userId));

    public async Task InvalidateRoleAsync(string roleId, CancellationToken ct = default)
    {
        var userIds = await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(ct);
        foreach (var userId in userIds)
            InvalidateUser(userId);
    }

    private static string CacheKey(string userId) => $"permissions:{userId}";

    private record EffectivePermissions(bool IsSuperAdmin, IReadOnlySet<string> Permissions);
}
