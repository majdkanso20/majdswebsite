namespace MajdsApp.SharedKernel.Localization;

/// <summary>A language the platform can speak (F-Localization). <see cref="Rtl"/> languages are laid out right to left.</summary>
public record LanguageInfo(string Code, string Label, bool Rtl);

/// <summary>
/// Translates the server's own user-facing text (F-Localization FR-I18N-002): error messages, notification texts and email
/// templates. English is the source text and the key, so a message with no translation (and every English request) is returned
/// unchanged and nothing is ever blank (AC-I18N-2). A message with a changing part is written once as a template such as
/// <c>Missing permission: {0}</c> and matched however the part is filled in.
/// The Shared Kernel ships a pass-through default; the Localization module replaces it with the real catalog.
/// </summary>
public interface IMessageCatalog
{
    /// <summary>The languages that can be chosen, with the default first.</summary>
    IReadOnlyList<LanguageInfo> Languages { get; }

    /// <summary>The text in <paramref name="culture"/> (for example <c>ar</c> or <c>ar-JO</c>), or the text itself when there is no translation.</summary>
    string Translate(string text, string culture);

    /// <summary>Every translation for one culture, keyed by the English text or template.</summary>
    IReadOnlyDictionary<string, string> GetResources(string culture);
}

public class NullMessageCatalog : IMessageCatalog
{
    public IReadOnlyList<LanguageInfo> Languages { get; } = [new LanguageInfo("en", "English", false)];

    public string Translate(string text, string culture) => text;

    public IReadOnlyDictionary<string, string> GetResources(string culture) => new Dictionary<string, string>();
}
