using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Exports;

public static class ExportSettingDefinitions
{
    public static class Exports
    {
        public static readonly SettingDefinition RetentionDays = new(
            "Exports.RetentionDays", "Exports", "Export retention (days)", SettingDataType.Integer, "7",
            description: "Finished background exports, and their files, are deleted after this many days.");
    }
}
