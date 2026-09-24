namespace MajdsApp.Modules.Settings;

/// <summary>Permission constants for F-Settings, discovered by <c>PermissionRegistry</c>'s assembly
/// scan the same way <c>MajdsApp.Modules.Authorization.Permissions</c> is (P2/Open-Closed).</summary>
public static class Permissions
{
    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Edit = "Settings.Edit";
    }
}
