using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Settings;

public record SettingDto(
    string Name,
    string Group,
    string DisplayName,
    SettingDataType DataType,
    string Value,
    string DefaultValue,
    string? Description,
    bool IsSensitive = false,
    bool HasValue = false);
