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
    public string? MinHostVersion { get; set; }
    public string? MaxHostVersion { get; set; }
    public List<PluginMenuEntryJson> Menu { get; set; } = [];
    public List<PluginDependencyJson> Dependencies { get; set; } = [];
    public PluginFrontendJson? Frontend { get; set; }
}

public class PluginDependencyJson
{
    public string Id { get; set; } = "";
    public string? MinVersion { get; set; }
    public string? MaxVersion { get; set; }
}

public class PluginMenuEntryJson
{
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Route { get; set; } = "";
    public string? Permission { get; set; }
    public int Order { get; set; }
    public string? Element { get; set; }
}

public class PluginFrontendJson
{
    public string Entry { get; set; } = "";
    public string? Styles { get; set; }
    public int Contract { get; set; }
    public int? Angular { get; set; }
}

/// <summary>One discovered plugin — either loaded successfully (<see cref="Assembly"/>/<see cref="Module"/>
/// populated) or failed (<see cref="LoadError"/> populated), never both (P5 FR-PLUG-003/008: a bad plugin
/// is rejected with a diagnostic, never a host crash).</summary>
public record LoadedPlugin(PluginManifest Manifest, Assembly? Assembly, IFeatureModule? Module, string? LoadError, string? Folder = null)
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

        // Staged installs, upgrades, rollbacks and uninstalls take effect now, before anything is loaded (see PluginInstaller).
        foreach (var message in PluginInstaller.ApplyPendingChanges(pluginsDirectory))
            logger?.LogInformation("Plugin change: {Message}", message);

        // Folders starting with a dot are the installer's own (.pending, .previous, .staging), never plugins.
        foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory).Where(d => !Path.GetFileName(d).StartsWith('.')))
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

        // A plugin whose dependencies are missing, too old or too new, or circular is left out with a diagnostic; the rest load in dependency order.
        return PluginDependencyResolver.Resolve(results);
    }

    internal static PluginManifest ToManifest(PluginManifestJson raw) =>
        new(raw.Id, raw.Name, raw.Version, raw.Author, raw.ModuleType,
            raw.Menu.Select(m => new PluginMenuEntry(m.Label, m.Icon, m.Route, m.Permission, m.Order, string.IsNullOrWhiteSpace(m.Element) ? null : m.Element)).ToList(),
            string.IsNullOrWhiteSpace(raw.MinHostVersion) ? null : raw.MinHostVersion,
            string.IsNullOrWhiteSpace(raw.MaxHostVersion) ? null : raw.MaxHostVersion,
            raw.Dependencies.Where(d => !string.IsNullOrWhiteSpace(d.Id))
                .Select(d => new PluginDependency(d.Id, string.IsNullOrWhiteSpace(d.MinVersion) ? null : d.MinVersion, string.IsNullOrWhiteSpace(d.MaxVersion) ? null : d.MaxVersion)).ToList(),
            raw.Frontend is { } f && !string.IsNullOrWhiteSpace(f.Entry)
                ? new PluginFrontend(f.Entry, string.IsNullOrWhiteSpace(f.Styles) ? null : f.Styles, f.Contract, f.Angular)
                : null);

    /// <summary>Null when the plugin supports this platform version; otherwise the reason it does not (P5 FR-PLUG-009).</summary>
    public static string? HostCompatibilityProblem(PluginManifest manifest)
    {
        var host = PluginHost.Version;

        if (manifest.MinHostVersion is not null)
        {
            if (!Version.TryParse(manifest.MinHostVersion, out var min))
                return $"minHostVersion '{manifest.MinHostVersion}' is not a valid version.";
            if (host < min) return $"This plugin needs platform version {min} or newer; this is {host}.";
        }

        if (manifest.MaxHostVersion is not null)
        {
            if (!Version.TryParse(manifest.MaxHostVersion, out var max))
                return $"maxHostVersion '{manifest.MaxHostVersion}' is not a valid version.";
            if (host > max) return $"This plugin supports platform versions up to {max}; this is {host}.";
        }

        return null;
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

        var manifest = ToManifest(raw);

        // A plugin built for another platform version is rejected with a diagnostic rather than loaded (P5 FR-PLUG-009).
        if (HostCompatibilityProblem(manifest) is { } incompatible)
            throw new InvalidOperationException(incompatible);

        // A menu entry that shows a custom element needs the bundle that defines it, and the bundle has to be in the package (FR-PLUG-014).
        if (manifest.Menu.Any(m => m.Element is not null) && manifest.Frontend is null)
            throw new InvalidOperationException("A menu entry names an element, but plugin.json has no frontend entry.");
        if (manifest.Frontend is { } frontend)
        {
            if (frontend.Contract < 1)
                throw new InvalidOperationException("The frontend needs a contract version (1 or later).");
            foreach (var file in new[] { frontend.Entry, frontend.Styles }.OfType<string>())
                if (PluginAssets.Resolve(new LoadedPlugin(manifest, null, null, null, pluginDir), file) is null)
                    throw new FileNotFoundException($"Frontend file '{file}' was not found in the package's frontend folder.");
        }

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

        return new LoadedPlugin(manifest, assembly, module, null, pluginDir);
    }
}
