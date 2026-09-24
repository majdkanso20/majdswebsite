using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using MediatR;

namespace MajdsApp.Modules.Settings;

/// <summary>Client-visible settings only (<see cref="SettingDefinition.IsVisibleToClient"/>), for parts
/// of the UI every signed-in user sees regardless of permissions — e.g. the shell header's application
/// name (F-Settings). Intentionally has no <c>[RequiresPermission]</c>: these values aren't sensitive.</summary>
public record GetPublicSettingsQuery : IRequest<IReadOnlyDictionary<string, string>>;

public class GetPublicSettingsQueryHandler(ISettingsProvider settingsProvider, ICurrentUser currentUser)
    : IRequestHandler<GetPublicSettingsQuery, IReadOnlyDictionary<string, string>>
{
    public async Task<IReadOnlyDictionary<string, string>> Handle(GetPublicSettingsQuery request, CancellationToken ct)
    {
        var effective = await settingsProvider.GetAllForUserAsync(currentUser.UserId, ct);
        var visibleNames = SettingDefinitionRegistry.GetAll().Where(d => d.IsVisibleToClient && !d.IsSensitive).Select(d => d.Name);

        return visibleNames.ToDictionary(name => name, name => effective.TryGetValue(name, out var value) ? value : string.Empty);
    }
}
