namespace MajdsApp.Modules.Authorization;

/// <summary>
/// Permission constants grouped by module (FR-AUTHZ-001). New feature modules add their own nested
/// static class here (or in their own project, discovered the same way) — never a magic string
/// scattered through handlers.
/// </summary>
public static class Permissions
{
    public static class Users
    {
        public const string View = "Users.View";
        public const string Create = "Users.Create";
        public const string Edit = "Users.Edit";
        public const string Delete = "Users.Delete";
    }

    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Delete = "Roles.Delete";
    }
}
