using System.Reflection;
using System.Runtime.Loader;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// Isolated, collectible load context per plugin (P5 FR-PLUG-006). Resolves a plugin's own private
/// dependencies from its output folder via <see cref="AssemblyDependencyResolver"/>, but returns
/// <c>null</c> (falling back to the default context) for anything it can't find locally — which is
/// exactly the shared types (SharedKernel/Core, MediatR, FluentValidation, EF Core, ASP.NET Core)
/// that the plugin's own <c>.csproj</c> references with <c>Private="false"</c> so they are NOT copied
/// into its bin folder. This is what keeps <c>typeof(IFeatureModule)</c> identical between host and
/// plugin: if the plugin loaded its own copy of SharedKernel.dll into this context, that copy's
/// <c>IFeatureModule</c> would be a distinct type from the host's, and every reflection-based
/// discovery mechanism (MediatR, Scrutor, the permission registry, EF entity configs) would silently
/// fail to recognize the plugin's types.
/// </summary>
public class PluginLoadContext(string pluginMainAssemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginMainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
