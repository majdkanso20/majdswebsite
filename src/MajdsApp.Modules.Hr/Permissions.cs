namespace MajdsApp.Modules.Hr;

/// <summary>One flat permission set for the whole module (Departments and Employees together), the same
/// shape as Files' Upload/View/Delete rather than a separate set per entity — reasonable for two closely
/// related lists edited by the same HR staff.</summary>
public static class Permissions
{
    public static class Hr
    {
        public const string View = "Hr.View";
        public const string Create = "Hr.Create";
        public const string Edit = "Hr.Edit";
        public const string Delete = "Hr.Delete";
    }
}
