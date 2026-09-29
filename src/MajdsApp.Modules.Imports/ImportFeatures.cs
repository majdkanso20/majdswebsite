using MajdsApp.SharedKernel.Features;

namespace MajdsApp.Modules.Imports;

public static class ImportFeatures
{
    public static class Flags
    {
        public static readonly FeatureDefinition Imports = new(
            "Imports", "Background imports", defaultEnabled: true,
            description: "Importing rows from an uploaded file as a background job.");
    }
}
