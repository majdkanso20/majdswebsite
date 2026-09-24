using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Jobs;

public static class JobSettings
{
    public static class Jobs
    {
        public static readonly SettingDefinition Enabled = new(
            "Jobs.Enabled", "Jobs", "Run scheduled background jobs", SettingDataType.Boolean, "true",
            description: "When off, jobs only run when started manually.");
    }
}
