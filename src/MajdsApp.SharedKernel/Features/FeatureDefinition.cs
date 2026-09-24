using System.Reflection;

namespace MajdsApp.SharedKernel.Features;

/// <summary>Code-defined feature flag (F-Features). Only an admin override is persisted; everything
/// else uses <see cref="DefaultEnabled"/>.</summary>
public sealed class FeatureDefinition(string name, string displayName, bool defaultEnabled = true, string? description = null)
{
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
    public bool DefaultEnabled { get; } = defaultEnabled;
    public string? Description { get; } = description;
}

/// <summary>Discovers feature definitions the same way permissions and settings are discovered: nested
/// static classes with public static <see cref="FeatureDefinition"/> fields in any MajdsApp.Modules.* assembly.</summary>
public static class FeatureDefinitionRegistry
{
    public static IReadOnlyList<FeatureDefinition> GetAll()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("MajdsApp.Modules.", StringComparison.Ordinal) == true);

        var result = new List<FeatureDefinition>();
        foreach (var assembly in assemblies)
        foreach (var outer in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true, IsNested: false }))
        foreach (var group in outer.GetNestedTypes(BindingFlags.Public).Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true }))
            result.AddRange(group.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(FeatureDefinition))
                .Select(f => (FeatureDefinition)f.GetValue(null)!));

        return result;
    }

    public static FeatureDefinition? Find(string name) => GetAll().FirstOrDefault(f => f.Name == name);
}

/// <summary>Resolves whether a feature is on. The Shared Kernel ships a defaults-only implementation;
/// F-Features replaces it with the database-backed one (same pattern as <see cref="Security.IPermissionChecker"/>).</summary>
public interface IFeatureChecker
{
    Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetEnabledAsync(CancellationToken ct = default);
    void Invalidate();
}

public class DefaultFeatureChecker : IFeatureChecker
{
    public Task<bool> IsEnabledAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(FeatureDefinitionRegistry.Find(name)?.DefaultEnabled ?? false);

    public Task<IReadOnlyCollection<string>> GetEnabledAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyCollection<string>>(
            FeatureDefinitionRegistry.GetAll().Where(f => f.DefaultEnabled).Select(f => f.Name).ToList());

    public void Invalidate() { }
}
