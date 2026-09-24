using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Settings;
using MediatR;

namespace MajdsApp.Modules.Settings;

/// <summary>Every setting definition merged with its effective value, for the admin settings page
/// (F-Settings). Requires the same permission as editing the role/permission tree — viewing raw
/// settings is itself sensitive (e.g. session timeout, self-registration).</summary>
[RequiresPermission(Permissions.Settings.View)]
public record GetSettingsQuery : IRequest<IReadOnlyList<SettingDto>>;

public class GetSettingsQueryHandler(ISettingsProvider settingsProvider) : IRequestHandler<GetSettingsQuery, IReadOnlyList<SettingDto>>
{
    public async Task<IReadOnlyList<SettingDto>> Handle(GetSettingsQuery request, CancellationToken ct)
    {
        var effective = await settingsProvider.GetAllAsync(ct);

        return SettingDefinitionRegistry.GetAll()
            .Where(d => !d.IsInternal)
            .OrderBy(d => d.Group).ThenBy(d => d.DisplayName)
            .Select(d =>
            {
                var value = effective.TryGetValue(d.Name, out var v) ? v : d.DefaultValue;
                // Sensitive values are write-only (AC-SET-2): report only whether one is set.
                return d.IsSensitive
                    ? new SettingDto(d.Name, d.Group, d.DisplayName, d.DataType, string.Empty, string.Empty, d.Description, true, value.Length > 0)
                    : new SettingDto(d.Name, d.Group, d.DisplayName, d.DataType, value, d.DefaultValue, d.Description);
            })
            .ToList();
    }
}
