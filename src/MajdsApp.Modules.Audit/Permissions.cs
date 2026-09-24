namespace MajdsApp.Modules.Audit;

/// <summary>Permission constants for F-Audit, discovered by <c>PermissionRegistry</c>'s assembly scan
/// the same way <c>MajdsApp.Modules.Authorization.Permissions</c> is (P2/Open-Closed).</summary>
public static class Permissions
{
    public static class Audit
    {
        public const string View = "Audit.View";
    }
}
