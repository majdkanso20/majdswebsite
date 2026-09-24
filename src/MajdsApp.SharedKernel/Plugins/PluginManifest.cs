namespace MajdsApp.SharedKernel.Plugins;

/// <summary>One entry in a plugin's declared navigation (P5 FR-PLUG-020). Consumed by the Angular
/// shell's menu registry (U3 FR-SHELL-008) exactly like a compiled-in module's static menu item.</summary>
public record PluginMenuEntry(string Label, string Icon, string Route, string? Permission, int Order);

/// <summary>
/// The subset of a plugin's <c>plugin.json</c> this host actually acts on (P5 FR-PLUG-002). Full P5
/// covers a much larger manifest (host-compatibility range, dependencies, settings definitions,
/// signature) — this project implements the core loadable-module + menu contribution slice.
/// </summary>
public record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string ModuleType,
    IReadOnlyList<PluginMenuEntry> Menu);
