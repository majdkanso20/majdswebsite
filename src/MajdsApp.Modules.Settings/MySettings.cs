using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Settings;

/// <summary>The settings a user may override themselves. <c>Value</c> is what applies to them now,
/// <c>DefaultValue</c> is what they would get with no personal override (the Application value).
/// Scoped to the caller by <see cref="ICurrentUser"/>, so it needs no permission.</summary>
public record GetMySettingsQuery : IRequest<IReadOnlyList<SettingDto>>;

public class GetMySettingsQueryHandler(ISettingsProvider settings, ICurrentUser currentUser)
    : IRequestHandler<GetMySettingsQuery, IReadOnlyList<SettingDto>>
{
    public async Task<IReadOnlyList<SettingDto>> Handle(GetMySettingsQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var mine = await settings.GetAllForUserAsync(userId, ct);
        var app = await settings.GetAllAsync(ct);

        return SettingDefinitionRegistry.GetAll()
            .Where(d => d.AllowUserOverride && !d.IsInternal)
            .OrderBy(d => d.Group).ThenBy(d => d.DisplayName)
            .Select(d => new SettingDto(d.Name, d.Group, d.DisplayName, d.DataType,
                mine.GetValueOrDefault(d.Name, d.DefaultValue), app.GetValueOrDefault(d.Name, d.DefaultValue), d.Description))
            .ToList();
    }
}

/// <summary>Saves the caller's own overrides. A value equal to the Application value is removed rather
/// than stored, so the user keeps following later Application changes.</summary>
public record UpdateMySettingsCommand(IReadOnlyList<SettingUpdateItem> Items) : IRequest, IAuditableCommand;

public class UpdateMySettingsCommandValidator : AbstractValidator<UpdateMySettingsCommand>
{
    public UpdateMySettingsCommandValidator()
    {
        RuleForEach(x => x.Items).ChildRules(item => item.RuleFor(i => i.Name).NotEmpty());
    }
}

public class UpdateMySettingsCommandHandler(
    ApplicationDbContext db, ISettingsProvider settings, ICurrentUser currentUser, MajdsApp.SharedKernel.Localization.IMessageCatalog catalog)
    : IRequestHandler<UpdateMySettingsCommand>
{
    public async Task Handle(UpdateMySettingsCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var definitions = SettingDefinitionRegistry.GetAll().ToDictionary(d => d.Name);
        var app = await settings.GetAllAsync(ct);
        var existing = await db.Set<SettingUserValue>().Where(s => s.UserId == userId).ToListAsync(ct);

        foreach (var item in request.Items)
        {
            if (!definitions.TryGetValue(item.Name, out var definition) || !definition.AllowUserOverride || definition.IsSensitive)
                throw new ValidationException($"'{item.Name}' cannot be set per user.");

            if (!IsValid(item.Value, definition.DataType))
                throw new ValidationException($"'{item.Value}' is not a valid value for '{item.Name}'.");

            SettingValueRules.Check(item.Name, item.Value, catalog);

            var row = existing.FirstOrDefault(s => s.Name == item.Name);
            var appValue = app.GetValueOrDefault(item.Name, definition.DefaultValue);

            if (item.Value == appValue)
            {
                if (row is not null) db.Set<SettingUserValue>().Remove(row);
            }
            else if (row is null)
            {
                db.Set<SettingUserValue>().Add(new SettingUserValue { UserId = userId, Name = item.Name, Value = item.Value });
            }
            else
            {
                row.Value = item.Value;
            }
        }

        await db.SaveChangesAsync(ct);
        settings.InvalidateUser(userId);
    }

    private static bool IsValid(string value, SettingDataType type) => type switch
    {
        SettingDataType.Boolean => bool.TryParse(value, out _),
        SettingDataType.Integer => int.TryParse(value, out _),
        _ => true
    };
}
