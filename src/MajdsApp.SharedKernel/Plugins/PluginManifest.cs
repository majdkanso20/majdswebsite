namespace MajdsApp.SharedKernel.Plugins;

/// <summary>One entry in a plugin's declared navigation (P5 FR-PLUG-020). Consumed by the Angular
/// shell's menu registry (U3 FR-SHELL-008) exactly like a compiled-in module's static menu item.</summary>
public record PluginMenuEntry(string Label, string Icon, string Route, string? Permission, int Order);

/// <summary>Another plugin this one needs (P5 FR-PLUG-010): its id and, optionally, the range of its versions that works with this plugin.</summary>
public record PluginDependency(string Id, string? MinVersion = null, string? MaxVersion = null);

/// <summary>
/// The subset of a plugin's <c>plugin.json</c> this host actually acts on (P5 FR-PLUG-002): identity, the loadable
/// module, its menu, and the host versions it supports (FR-PLUG-009). Full P5 covers a larger manifest
/// (settings definitions, signature); those are not implemented.
/// </summary>
public record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string ModuleType,
    IReadOnlyList<PluginMenuEntry> Menu,
    string? MinHostVersion = null,
    string? MaxHostVersion = null,
    IReadOnlyList<PluginDependency>? Dependencies = null)
{
    /// <summary>The plugins this one needs; never null.</summary>
    public IReadOnlyList<PluginDependency> DependsOn => Dependencies ?? [];
}
