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
    bool isSensitive = false)
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
}
