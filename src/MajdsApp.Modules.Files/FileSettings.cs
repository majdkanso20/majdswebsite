using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Files;

public static class FileSettings
{
    public static class Files
    {
        public static readonly SettingDefinition MaxUploadMb = new(
            "Files.MaxUploadMb", "Files", "Maximum upload size (MB)", SettingDataType.Integer, "10",
            description: "Uploads larger than this are rejected.");
    }
}
