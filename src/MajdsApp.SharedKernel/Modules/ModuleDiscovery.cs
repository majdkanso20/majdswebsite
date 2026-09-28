using System.Reflection;
using System.Runtime.Loader;

namespace MajdsApp.SharedKernel.Modules;

/// <summary>
/// Finds the feature modules the host was built with (P2 FR-MOD-002/008), so the host does not keep a list of them. A module is a project named
/// <c>MajdsApp.Modules.*</c>; referencing it from the host project is all it takes to be included, and removing the reference removes it.
/// </summary>
public static class ModuleDiscovery
{
    public const string Prefix = "MajdsApp.Modules.";

    /// <summary>Every <c>MajdsApp.Modules.*</c> assembly next to the application, loaded, in name order.</summary>
    public static Assembly[] LoadModuleAssemblies(string? directory = null)
    {
        directory ??= AppContext.BaseDirectory;
        var assemblies = new List<Assembly>();

        foreach (var path in Directory.EnumerateFiles(directory, Prefix + "*.dll").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var name = AssemblyName.GetAssemblyName(path);
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name.Name)
                ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            assemblies.Add(loaded);
        }

        return [.. assemblies];
    }
}
