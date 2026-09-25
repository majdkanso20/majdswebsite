using System.Reflection;

namespace MajdsApp.Modules.Authorization;

public record PermissionGroup(string Name, IReadOnlyList<string> Permissions);

/// <summary>
/// Exposes every defined permission grouped by module (FR-AUTHZ-002) for the role editor. Scans all
/// loaded module assemblies for nested static classes of public const strings — the same convention
/// as <see cref="Permissions"/> — so a new feature module's permissions appear automatically with no
/// change here (P2/Open-Closed).
/// </summary>
public static class PermissionRegistry
{
    public static IReadOnlyList<PermissionGroup> GetAllGroups()
    {
        // Includes MajdsApp.Plugins.* too (P5 FR-AUTHZ-008): a plugin's permissions are declared the
        // exact same way as a compiled-in module's, so once its assembly is loaded (PluginManager),
        // they appear here — and in the role editor — automatically, with no plugin-specific code.
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } name
                && (name.StartsWith("MajdsApp.Modules.", StringComparison.Ordinal) || name.StartsWith("MajdsApp.Plugins.", StringComparison.Ordinal)));

        var groups = new List<PermissionGroup>();
        foreach (var assembly in moduleAssemblies)
            groups.AddRange(GetGroups(assembly));

        return groups;
    }

    /// <summary>The permission groups one assembly declares. Used to find what an uninstalled plugin must give back (P5 FR-PLUG-028).</summary>
    public static IReadOnlyList<PermissionGroup> GetGroups(Assembly assembly)
    {
        var groups = new List<PermissionGroup>();

        foreach (var outerType in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true, IsNested: false }))
        {
            foreach (var groupType in outerType.GetNestedTypes(BindingFlags.Public).Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true }))
            {
                var permissionNames = groupType
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                    .Select(f => (string)f.GetRawConstantValue()!)
                    .ToList();

                if (permissionNames.Count > 0)
                    groups.Add(new PermissionGroup(groupType.Name, permissionNames));
            }
        }

        return groups;
    }

    public static IReadOnlyList<string> GetPermissionNames(Assembly assembly) =>
        GetGroups(assembly).SelectMany(g => g.Permissions).Distinct().ToList();

    public static IReadOnlyList<string> GetAllPermissionNames() =>
        GetAllGroups().SelectMany(g => g.Permissions).Distinct().ToList();
}
