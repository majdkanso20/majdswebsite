using System.Reflection;

namespace MajdsApp.SharedKernel.Settings;

/// <summary>
/// Scans every loaded module assembly for nested static classes exposing public static
/// <see cref="SettingDefinition"/> fields — the same convention <c>PermissionRegistry</c> uses for
/// permissions — so a new feature module's settings appear automatically (P2/Open-Closed).
/// </summary>
public static class SettingDefinitionRegistry
{
    public static IReadOnlyList<SettingDefinition> GetAll()
    {
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("MajdsApp.Modules.", StringComparison.Ordinal) == true);

        var definitions = new List<SettingDefinition>();

        foreach (var assembly in moduleAssemblies)
        {
            foreach (var outerType in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true, IsNested: false }))
            {
                foreach (var groupType in outerType.GetNestedTypes(BindingFlags.Public).Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true }))
                {
                    var values = groupType
                        .GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Where(f => f.FieldType == typeof(SettingDefinition))
                        .Select(f => (SettingDefinition)f.GetValue(null)!);

                    definitions.AddRange(values);
                }
            }
        }

        return definitions;
    }

    public static SettingDefinition? Find(string name) => GetAll().FirstOrDefault(d => d.Name == name);
}
