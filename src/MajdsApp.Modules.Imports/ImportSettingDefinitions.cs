using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Imports;

public static class ImportSettingDefinitions
{
    public static class Imports
    {
        public static readonly SettingDefinition RetentionDays = new(
            "Imports.RetentionDays", "Imports", "Import history retention (days)", SettingDataType.Integer, "30",
            description: "Finished import jobs (and the uploaded file each held) are deleted after this many days.");
    }
}
