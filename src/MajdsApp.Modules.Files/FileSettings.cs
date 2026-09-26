using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Files;

public static class FileSettings
{
    public static class Files
    {
        public static readonly SettingDefinition MaxUploadMb = new(
            "Files.MaxUploadMb", "Files", "Maximum upload size (MB)", SettingDataType.Integer, "10",
            description: "Uploads larger than this are rejected.");

        public static readonly SettingDefinition DeletedRetentionDays = new(
            "Files.DeletedRetentionDays", "Files", "Deleted files kept for (days)", SettingDataType.Integer, "30",
            description: "A deleted file is hidden at once and physically removed after this many days (1 to 3650).",
            validator: value => int.TryParse(value, out var n) && n is >= 1 and <= 3650 ? null : "Deleted files must be kept for between 1 and 3650 days.");
    }
}
