namespace MajdsApp.SharedKernel.Security;

/// <summary>
/// Checked by <c>AuthorizationBehavior</c> against <see cref="Behaviors.RequiresPermissionAttribute"/>.
/// The Shared Kernel ships a permissive default so Phase 0 compiles and runs before F-Authorization
/// exists; the F-Authorization module replaces this registration with the real permission-tree check.
/// </summary>
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);
}

/// <summary>Default no-op used until F-Authorization registers the real implementation.</summary>
public class AllowAllPermissionChecker : IPermissionChecker
{
    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default) => Task.FromResult(true);
}
