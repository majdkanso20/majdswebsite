using System.Text.Json;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// A plugin's own translations (P5 FR-PLUG-023): <c>localization/&lt;language&gt;.json</c> in the plugin's folder, a flat object keyed by the
/// English text (a menu label, a message the plugin writes), in the same format as the platform's files. A file that is missing or is not
/// a flat object of strings is ignored: an untranslated plugin shows its English text, and a broken file never stops the host.
/// </summary>
public static class PluginTranslations
{
    public static IReadOnlyDictionary<string, string> Load(LoadedPlugin plugin, string language)
    {
        if (!plugin.Succeeded || plugin.Folder is null || language.Any(c => !char.IsLetter(c))) return new Dictionary<string, string>();

        var path = Path.Combine(plugin.Folder, "localization", language.ToLowerInvariant() + ".json");
        if (!File.Exists(path)) return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            return new Dictionary<string, string>();
        }
    }
}
