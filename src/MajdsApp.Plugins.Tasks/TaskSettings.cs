using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Plugins.Tasks;

/// <summary>
/// The plugin's own settings (P5 FR-PLUG-013). They are found by the same scan as the platform's, so they appear on the standard settings page
/// grouped under "Tasks", are stored and cached like any setting, and are checked by the rule given here when an administrator saves them.
/// A plugin may only define names that start with its own key (here "Tasks.").
/// </summary>
public static class TaskSettingDefinitions
{
    public static class Tasks
    {
        public const int MaxTitleLengthLimit = 500;

        public static readonly SettingDefinition MaxTitleLength = new(
            "Tasks.MaxTitleLength", "Tasks", "Longest task title", SettingDataType.Integer, "200",
            description: "How many characters a task title may have (1 to 500).",
            validator: value => int.TryParse(value, out var n) && n is >= 1 and <= MaxTitleLengthLimit
                ? null
                : "The longest task title must be between 1 and 500.");
    }
}
