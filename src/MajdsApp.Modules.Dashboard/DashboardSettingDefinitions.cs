using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Dashboard;

public static class DashboardSettingDefinitions
{
    public static class Dashboard
    {
        /// <summary>Per-user layout (FR-DASH-004) kept as a User-scope setting (F-Settings): JSON
        /// <c>{"order":["key",...],"hidden":["key",...]}</c>. Written by the dashboard's Customize panel, so it is
        /// not listed on the settings screens.</summary>
        public static readonly SettingDefinition Layout = new(
            "Dashboard.Layout", "Dashboard", "Dashboard layout", SettingDataType.String, "",
            description: "Order and visibility of the dashboard widgets.",
            allowUserOverride: true, isInternal: true);
    }
}
