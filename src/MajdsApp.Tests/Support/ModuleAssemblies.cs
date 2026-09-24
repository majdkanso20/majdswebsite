using System.Reflection;

namespace MajdsApp.Tests.Support;

/// <summary>The permission and setting registries scan the assemblies loaded in the process. The host loads
/// every module at startup; a bare unit test must do the same to see the whole platform.</summary>
public static class ModuleAssemblies
{
    public static void LoadAll()
    {
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "MajdsApp.Modules.*.dll"))
            Assembly.LoadFrom(dll);
    }
}
