using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Plugins;

namespace MajdsApp.Modules.Localization;

/// <summary>
/// The server's translations (F-Localization FR-I18N-001/002). One embedded JSON file per language under <c>Resources/</c>, keyed by the
/// English text. An entry with <c>{0}</c>, <c>{1}</c> ... is a template: it matches any message of that shape (for example
/// <c>Missing permission: Users.View</c> matches <c>Missing permission: {0}</c>) and the same values are put into the translation. Anything
/// with no entry, and every request in English, comes back unchanged, so a missing translation shows the English source, never a blank (AC-I18N-2).
/// </summary>
public sealed partial class MessageCatalog : IMessageCatalog
{
    /// <summary>The languages on offer. English is the source text and is listed first as the default.</summary>
    public static readonly IReadOnlyList<LanguageInfo> Supported =
    [
        new("en", "English", false),
        new("ar", "العربية", true)
    ];

    private sealed record Template(Regex Pattern, string Translation, int LiteralLength);

    private readonly Dictionary<string, Dictionary<string, string>> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Template>> _templates = new(StringComparer.OrdinalIgnoreCase);

    public MessageCatalog(IReadOnlyList<LoadedPlugin>? plugins = null)
    {
        foreach (var language in Supported.Where(l => l.Code != "en"))
        {
            var entries = Load(language.Code);

            // A plugin adds its own entries (FR-PLUG-023) but can never replace one the platform already translated.
            foreach (var plugin in plugins ?? [])
                foreach (var (key, value) in PluginTranslations.Load(plugin, language.Code))
                    entries.TryAdd(key, value);

            _exact[language.Code] = entries;
            _templates[language.Code] = entries
                .Where(e => Placeholder().IsMatch(e.Key))
                .Select(e => new Template(ToPattern(e.Key), e.Value, Placeholder().Replace(e.Key, "").Length))
                .OrderByDescending(t => t.LiteralLength) // the most specific template wins
                .ToList();
        }
    }

    public IReadOnlyList<LanguageInfo> Languages => Supported;

    public string Translate(string text, string culture)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var language = LanguageOf(culture);
        if (language is null || !_exact.TryGetValue(language, out var entries)) return text;

        if (entries.TryGetValue(text, out var exact)) return exact;

        foreach (var template in _templates[language])
        {
            var match = template.Pattern.Match(text);
            if (!match.Success) continue;

            var result = template.Translation;
            for (var i = 1; i < match.Groups.Count; i++)
                result = result.Replace("{" + (i - 1) + "}", match.Groups[i].Value);
            return result;
        }

        return text;
    }

    public IReadOnlyDictionary<string, string> GetResources(string culture) =>
        LanguageOf(culture) is { } language && _exact.TryGetValue(language, out var entries) ? entries : new Dictionary<string, string>();

    /// <summary>"ar-JO" and "AR" both mean "ar"; anything unsupported means English (null).</summary>
    public static string? LanguageOf(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return null;
        var code = culture.Split('-', '_')[0].Trim().ToLowerInvariant();
        return Supported.Any(l => l.Code == code && code != "en") ? code : null;
    }

    private static Regex ToPattern(string template)
    {
        var parts = Placeholder().Split(template).Select(Regex.Escape);
        return new Regex("^" + string.Join("(.+?)", parts) + "$", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static Dictionary<string, string> Load(string code)
    {
        var assembly = typeof(MessageCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($".Resources.{code}.json", StringComparison.OrdinalIgnoreCase));
        if (name is null) return new Dictionary<string, string>();

        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();
}
