using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Settings;
using MediatR;

namespace MajdsApp.Modules.Settings;

/// <param name="Clear">Only meaningful for sensitive settings: removes the stored secret. Otherwise a blank sensitive value means "leave unchanged".</param>
public record SettingUpdateItem(string Name, string Value, bool Clear = false);

/// <summary>Bulk-saves the settings admin page (F-Settings). A value equal to the definition's default
/// is not persisted as an override — it's deleted instead, keeping the table to actual overrides only.</summary>
[RequiresPermission(Permissions.Settings.Edit)]
public record UpdateSettingsCommand(IReadOnlyList<SettingUpdateItem> Items) : IRequest, ITransactionalCommand, IAuditableCommand;

public class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        RuleForEach(x => x.Items).ChildRules(item => item.RuleFor(i => i.Name).NotEmpty());
    }
}

public class UpdateSettingsCommandHandler(
    ApplicationDbContext db, ISettingsProvider settingsProvider, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtection,
    MajdsApp.SharedKernel.Localization.IMessageCatalog catalog)
    : IRequestHandler<UpdateSettingsCommand>
{
    public async Task Handle(UpdateSettingsCommand request, CancellationToken ct)
    {
        var definitionsByName = SettingDefinitionRegistry.GetAll().ToDictionary(d => d.Name);

        foreach (var item in request.Items)
        {
            if (!definitionsByName.TryGetValue(item.Name, out var definition))
                throw new ValidationException($"Unknown setting '{item.Name}'.");

            if (!IsValidForType(item.Value, definition.DataType))
                throw new ValidationException($"'{item.Value}' is not a valid value for '{item.Name}'.");

            if (!definition.IsSensitive) SettingValueRules.Check(item.Name, item.Value, catalog);
            if (!definition.IsSensitive && definition.Validate(item.Value) is { } problem) throw new ValidationException(problem);

            var existing = await db.Set<SettingValue>().FindAsync([item.Name], ct);

            if (definition.IsSensitive)
            {
                if (item.Clear)
                {
                    if (existing is not null) db.Set<SettingValue>().Remove(existing);
                }
                else if (item.Value.Length > 0)
                {
                    var protectedValue = SettingSecrets.Protect(dataProtection, item.Value);
                    if (existing is null) db.Set<SettingValue>().Add(new SettingValue { Id = item.Name, Value = protectedValue });
                    else existing.Value = protectedValue;
                }
                continue;
            }

            if (item.Value == definition.DefaultValue)
            {
                if (existing is not null)
                    db.Set<SettingValue>().Remove(existing);
                continue;
            }

            if (existing is null)
                db.Set<SettingValue>().Add(new SettingValue { Id = item.Name, Value = item.Value });
            else
                existing.Value = item.Value;
        }

        settingsProvider.Invalidate();
    }

    private static bool IsValidForType(string value, SettingDataType dataType) => dataType switch
    {
        SettingDataType.Boolean => bool.TryParse(value, out _),
        SettingDataType.Integer => int.TryParse(value, out _),
        _ => true
    };
}
