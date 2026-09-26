namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// Orders plugins so every plugin comes after the ones it depends on, and rejects the ones that cannot be satisfied (P5 FR-PLUG-010): a
/// dependency that is not installed, not loaded, outside the version range asked for, or part of a cycle. A rejected plugin is not
/// partly loaded: it is reported with its reason and its assembly is not used, and so are the plugins that depend on it.
/// </summary>
public static class PluginDependencyResolver
{
    public static IReadOnlyList<LoadedPlugin> Resolve(IReadOnlyList<LoadedPlugin> plugins)
    {
        var byId = plugins.GroupBy(p => p.Manifest.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var problems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<LoadedPlugin>();
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 1 = being visited, 2 = done

        void Visit(LoadedPlugin plugin, Stack<string> path)
        {
            var id = plugin.Manifest.Id;
            if (state.TryGetValue(id, out var s))
            {
                if (s == 1)
                {
                    var cycle = path.Reverse().SkipWhile(x => !x.Equals(id, StringComparison.OrdinalIgnoreCase)).Append(id).ToList();
                    foreach (var member in cycle) problems[member] = $"Dependency cycle: {string.Join(" -> ", cycle)}.";
                }
                return;
            }

            state[id] = 1;
            path.Push(id);
            foreach (var dependency in plugin.Manifest.DependsOn)
            {
                if (!byId.TryGetValue(dependency.Id, out var found))
                {
                    problems.TryAdd(id, $"Needs plugin '{dependency.Id}', which is not installed.");
                    continue;
                }

                Visit(found, path);

                if (!found.Succeeded)
                    problems.TryAdd(id, $"Needs plugin '{dependency.Id}', which could not be loaded.");
                else if (problems.TryGetValue(dependency.Id, out var inner))
                    problems.TryAdd(id, $"Needs plugin '{dependency.Id}', which cannot be used: {inner}");
                else if (VersionProblem(dependency, found.Manifest.Version) is { } version)
                    problems.TryAdd(id, version);
            }

            path.Pop();
            state[id] = 2;
            ordered.Add(plugin);
        }

        foreach (var plugin in plugins) Visit(plugin, new Stack<string>());

        return ordered.Select(p => p.Succeeded && problems.TryGetValue(p.Manifest.Id, out var reason)
            ? new LoadedPlugin(p.Manifest, null, null, reason)
            : p).ToList();
    }

    private static string? VersionProblem(PluginDependency dependency, string actual)
    {
        if (!Version.TryParse(actual, out var have))
            return $"Needs plugin '{dependency.Id}', whose version '{actual}' is not a valid version.";

        if (dependency.MinVersion is not null)
        {
            if (!Version.TryParse(dependency.MinVersion, out var min)) return $"The minimum version '{dependency.MinVersion}' for '{dependency.Id}' is not a valid version.";
            if (have < min) return $"Needs plugin '{dependency.Id}' {min} or newer; {have} is installed.";
        }

        if (dependency.MaxVersion is not null)
        {
            if (!Version.TryParse(dependency.MaxVersion, out var max)) return $"The maximum version '{dependency.MaxVersion}' for '{dependency.Id}' is not a valid version.";
            if (have > max) return $"Needs plugin '{dependency.Id}' up to {max}; {have} is installed.";
        }

        return null;
    }
}
