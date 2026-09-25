using System.Text.RegularExpressions;
using FluentValidation;
using MajdsApp.SharedKernel.Localization;

namespace MajdsApp.Modules.Settings;

/// <summary>The rules a setting's value must meet beyond its type, shared by the personal and the application-wide update so both
/// refuse the same things: a language must be one the platform speaks, a time zone must exist, a currency must look like an ISO 4217 code.</summary>
public static partial class SettingValueRules
{
    public static void Check(string name, string value, IMessageCatalog catalog)
    {
        switch (name)
        {
            case "General.DefaultLanguage" when !catalog.Languages.Any(l => l.Code.Equals(value, StringComparison.OrdinalIgnoreCase)):
                throw new ValidationException($"'{value}' is not a supported language.");

            case "Appearance.Timezone" when !TimeZoneInfo.TryFindSystemTimeZoneById(value, out _):
                throw new ValidationException($"'{value}' is not a known time zone.");

            case "Appearance.Currency" when !CurrencyCode().IsMatch(value):
                throw new ValidationException($"'{value}' is not a valid currency code (for example USD).");
        }
    }

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CurrencyCode();
}
