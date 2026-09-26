namespace MajdsApp.SharedKernel.Settings;

public enum SettingDataType
{
    String,
    Boolean,
    Integer
}

/// <summary>
/// Code-defined description of a configurable setting (F-Settings). The default value lives here;
/// only an override, when an admin changes it, is persisted to the database (F-Settings data model).
/// </summary>
public sealed class SettingDefinition(
    string name,
    string group,
    string displayName,
    SettingDataType dataType,
    string defaultValue,
    bool isVisibleToClient = false,
    string? description = null,
    bool allowUserOverride = false,
    bool isSensitive = false,
    bool isInternal = false,
    Func<string, string?>? validator = null)
{
    public string Name { get; } = name;
    public string Group { get; } = group;
    public string DisplayName { get; } = displayName;
    public SettingDataType DataType { get; } = dataType;
    public string DefaultValue { get; } = defaultValue;
    public bool IsVisibleToClient { get; } = isVisibleToClient;
    public string? Description { get; } = description;

    /// <summary>Scopes at which this may be overridden (FR-SET-001): false = Application only, true = a user may also set their own value.</summary>
    public bool AllowUserOverride { get; } = allowUserOverride;

    /// <summary>Credentials and similar (FR-SET-006): stored encrypted, never returned by any API, and
    /// changed write-only. Server code still reads the plain value through <c>ISettingsProvider</c>.</summary>
    public bool IsSensitive { get; } = isSensitive;

    /// <summary>Stored like any setting but written by a feature's own screen (for example the dashboard layout),
    /// so it is not listed on the settings pages.</summary>
    public bool IsInternal { get; } = isInternal;

    /// <summary>Optional rule for the value, beyond its type: returns the message to show when the value is not acceptable, or null when it is.
    /// A plugin uses this to validate its own settings (P5 FR-PLUG-013).</summary>
    public string? Validate(string value) => validator?.Invoke(value);
}
