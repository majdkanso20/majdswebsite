namespace MajdsApp.Plugins.Tasks;

/// <summary>
/// Declared like any compiled-in module's permissions (same nested-static-class convention the
/// reflection-based PermissionRegistry already scans) — nothing plugin-specific is needed for these
/// to appear, grouped under "Tasks", in the role editor once this assembly is loaded (P5 FR-AUTHZ-008).
/// </summary>
public static class Permissions
{
    public static class Tasks
    {
        public const string View = "Tasks.View";
        public const string Create = "Tasks.Create";
        public const string Edit = "Tasks.Edit";
        public const string Delete = "Tasks.Delete";
    }
}
