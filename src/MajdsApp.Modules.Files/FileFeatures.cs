using MajdsApp.SharedKernel.Features;

namespace MajdsApp.Modules.Files;

public static class FileFeatures
{
    public static class Flags
    {
        public static readonly FeatureDefinition Files = new(
            "Files", "File storage", defaultEnabled: true,
            description: "Uploading, listing and downloading files.");
    }
}
