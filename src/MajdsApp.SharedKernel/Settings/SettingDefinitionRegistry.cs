using System.Reflection;

namespace MajdsApp.SharedKernel.Settings;

/// <summary>
/// Scans every loaded module assembly for nested static classes exposing public static
/// <see cref="SettingDefinition"/> fields — the same convention <c>PermissionRegistry</c> uses for
/// permissions — so a new feature module's settings appear automatically (P2/Open-Closed).
/// </summary>
public static class SettingDefinitionRegistry
{
    private const string ModulePrefix = "MajdsApp.Modules.";
    private const string PluginPrefix = "MajdsApp.Plugins.";

    public static IReadOnlyList<SettingDefinition> GetAll()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => (Assembly: a, Name: a.GetName().Name ?? string.Empty))
            .Where(a => a.Name.StartsWith(ModulePrefix, StringComparison.Ordinal) || a.Name.StartsWith(PluginPrefix, StringComparison.Ordinal))
            .OrderBy(a => a.Name.StartsWith(ModulePrefix, StringComparison.Ordinal) ? 0 : 1); // the platform's own settings win any clash

        var definitions = new List<SettingDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (assembly, assemblyName) in assemblies)
        {
            // A plugin's settings are namespaced by the plugin (P5 FR-PLUG-013): a plugin named MajdsApp.Plugins.Tasks may only define
            // "Tasks.*", so it can neither replace a platform setting nor another plugin's.
            var pluginKey = assemblyName.StartsWith(PluginPrefix, StringComparison.Ordinal) ? assemblyName[PluginPrefix.Length..] : null;

            foreach (var outerType in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true, IsNested: false }))
            {
                foreach (var groupType in outerType.GetNestedTypes(BindingFlags.Public).Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true }))
                {
                    var values = groupType
                        .GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Where(f => f.FieldType == typeof(SettingDefinition))
                        .Select(f => (SettingDefinition)f.GetValue(null)!);

                    foreach (var definition in values)
                    {
                        if (pluginKey is not null && !definition.Name.StartsWith(pluginKey + ".", StringComparison.Ordinal)) continue;
                        if (seen.Add(definition.Name)) definitions.Add(definition);
                    }
                }
            }
        }

        return definitions;
    }

    public static SettingDefinition? Find(string name) => GetAll().FirstOrDefault(d => d.Name == name);
}
