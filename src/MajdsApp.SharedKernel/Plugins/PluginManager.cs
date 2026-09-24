using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>Raw shape of <c>plugin.json</c> (P5 FR-PLUG-002), deserialized before being turned into a <see cref="PluginManifest"/>.</summary>
public class PluginManifestJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Assembly { get; set; } = "";
    public string ModuleType { get; set; } = "";
    public List<PluginMenuEntryJson> Menu { get; set; } = [];
}

public class PluginMenuEntryJson
{
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Route { get; set; } = "";
    public string? Permission { get; set; }
    public int Order { get; set; }
}

/// <summary>One discovered plugin — either loaded successfully (<see cref="Assembly"/>/<see cref="Module"/>
/// populated) or failed (<see cref="LoadError"/> populated), never both (P5 FR-PLUG-003/008: a bad plugin
/// is rejected with a diagnostic, never a host crash).</summary>
public record LoadedPlugin(PluginManifest Manifest, Assembly? Assembly, IFeatureModule? Module, string? LoadError)
{
    public bool Succeeded => LoadError is null;
}

/// <summary>
/// Discovers plugins from a folder at host startup (P5 FR-PLUG-005) — each subfolder with a
/// <c>plugin.json</c> + <c>backend/&lt;assembly&gt;</c> is one plugin. Each is loaded into its own
/// isolated <see cref="PluginLoadContext"/> (FR-PLUG-006) so a broken or incompatible plugin never
/// takes down the host or another plugin (FR-PLUG-008) — this is why every step below is wrapped in
/// its own try/catch rather than one catch around the whole scan.
/// </summary>
public static class PluginManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<LoadedPlugin> LoadAll(string pluginsDirectory, ILogger? logger = null)
    {
        var results = new List<LoadedPlugin>();
        if (!Directory.Exists(pluginsDirectory))
            return results;

        foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory))
        {
            var fallbackId = Path.GetFileName(pluginDir);
            try
            {
                results.Add(LoadOne(pluginDir));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load plugin from '{Dir}'", pluginDir);
                results.Add(new LoadedPlugin(
                    new PluginManifest(fallbackId, fallbackId, "0.0.0", "", "", []),
                    null, null, ex.Message));
            }
        }

        return results;
    }

    private static LoadedPlugin LoadOne(string pluginDir)
    {
        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("plugin.json not found.", manifestPath);

        var raw = JsonSerializer.Deserialize<PluginManifestJson>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException("plugin.json is empty or invalid.");

        if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.ModuleType) || string.IsNullOrWhiteSpace(raw.Assembly))
            throw new InvalidOperationException("plugin.json must declare id, assembly, and moduleType.");

        var manifest = new PluginManifest(raw.Id, raw.Name, raw.Version, raw.Author, raw.ModuleType,
            raw.Menu.Select(m => new PluginMenuEntry(m.Label, m.Icon, m.Route, m.Permission, m.Order)).ToList());

        var assemblyPath = Path.Combine(pluginDir, "backend", raw.Assembly);
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Plugin assembly '{raw.Assembly}' not found.", assemblyPath);

        var context = new PluginLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        var moduleType = assembly.GetType(raw.ModuleType)
            ?? throw new InvalidOperationException($"Type '{raw.ModuleType}' not found in {raw.Assembly}.");

        if (!typeof(IFeatureModule).IsAssignableFrom(moduleType))
            throw new InvalidOperationException($"'{raw.ModuleType}' does not implement IFeatureModule.");

        var module = (IFeatureModule)Activator.CreateInstance(moduleType)!;

        return new LoadedPlugin(manifest, assembly, module, null);
    }
}
