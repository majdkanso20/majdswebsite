using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Plugins;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Plugins;

public record PluginDto(
    string Id, string Name, string Version, string Author, bool IsEnabled, string? LastError, DateTime DiscoveredAt,
    IReadOnlyList<string> Permissions, IReadOnlyList<string> MenuEntries, bool CanRollback, bool PendingUninstall,
    string? MinHostVersion, string? MaxHostVersion, IReadOnlyList<string> Dependencies);

[RequiresPermission(Permissions.Plugins.View)]
public record ListPluginsQuery : IRequest<IReadOnlyList<PluginDto>>;

public class ListPluginsQueryHandler(ApplicationDbContext db, IReadOnlyList<LoadedPlugin> loaded, PluginHostOptions options)
    : IRequestHandler<ListPluginsQuery, IReadOnlyList<PluginDto>>
{
    public async Task<IReadOnlyList<PluginDto>> Handle(ListPluginsQuery request, CancellationToken ct)
    {
        var rows = await db.Set<InstalledPlugin>().AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        var pendingUninstall = PluginInstaller.ListPending(options.Directory).Where(c => c.Action == "Uninstall").Select(c => c.Id).ToHashSet();

        return rows.Select(p =>
        {
            var plugin = loaded.FirstOrDefault(l => l.Manifest.Id == p.Id);
            var permissions = plugin?.Assembly is { } assembly ? MajdsApp.Modules.Authorization.PermissionRegistry.GetPermissionNames(assembly) : [];
            return new PluginDto(p.Id, p.Name, p.Version, p.Author, p.IsEnabled, p.LastError, p.DiscoveredAt,
                permissions, plugin?.Manifest.Menu.Select(m => m.Label).ToList() ?? [],
                PluginInstaller.HasPreviousVersion(options.Directory, p.Id), pendingUninstall.Contains(p.Id),
                plugin?.Manifest.MinHostVersion, plugin?.Manifest.MaxHostVersion,
                plugin?.Manifest.DependsOn.Select(d => d.Id).ToList() ?? []);
        }).ToList();
    }
}

[RequiresPermission(Permissions.Plugins.Manage)]
public record SetPluginEnabledCommand(string PluginId, bool Enabled) : IRequest, IAuditableCommand;

public class SetPluginEnabledCommandValidator : AbstractValidator<SetPluginEnabledCommand>
{
    public SetPluginEnabledCommandValidator()
    {
        RuleFor(x => x.PluginId).NotEmpty();
    }
}

public class SetPluginEnabledCommandHandler(
    ApplicationDbContext db, PluginStateCache cache, IReadOnlyList<LoadedPlugin> loaded, IServiceProvider services, ILogger<SetPluginEnabledCommandHandler> logger)
    : IRequestHandler<SetPluginEnabledCommand>
{
    public async Task Handle(SetPluginEnabledCommand request, CancellationToken ct)
    {
        var plugin = await db.Set<InstalledPlugin>().FirstOrDefaultAsync(p => p.Id == request.PluginId, ct)
            ?? throw new NotFoundException($"Plugin '{request.PluginId}' was not found.");
        if (plugin.IsEnabled == request.Enabled) return;

        var running = loaded.FirstOrDefault(l => l.Manifest.Id == plugin.Id);
        var enabledIds = (await db.Set<InstalledPlugin>().Where(p => p.IsEnabled).Select(p => p.Id).ToListAsync(ct)).ToHashSet();

        if (request.Enabled)
        {
            // FR-PLUG-010: a plugin is only switched on when everything it needs is on.
            var off = running?.Manifest.DependsOn.Where(d => !enabledIds.Contains(d.Id)).Select(d => d.Id).ToList() ?? [];
            var needed = string.Join(", ", off);
            if (off.Count > 0)
                throw new ConflictException($"'{plugin.Name}' needs {needed}, which must be enabled first.");
            if (running is { Succeeded: false })
                throw new ConflictException($"'{plugin.Name}' could not be loaded and cannot be enabled: {running.LoadError}");

            // FR-PLUG-031: a failing OnEnable leaves the plugin disabled and tells the administrator why.
            if (running is not null && await PluginHooks.RunAsync(running, services, (h, c) => h.OnEnableAsync(c, ct), "OnEnable", logger) is { } failure)
                throw new ConflictException($"'{plugin.Name}' was not enabled. {failure}");
        }
        else
        {
            var dependents = loaded.Where(l => l.Succeeded && enabledIds.Contains(l.Manifest.Id) && l.Manifest.DependsOn.Any(d => d.Id == plugin.Id))
                .Select(l => l.Manifest.Name).ToList();
            var dependentNames = string.Join(", ", dependents);
            if (dependents.Count > 0)
                throw new ConflictException($"'{plugin.Name}' is needed by {dependentNames}; disable those first.");
        }

        plugin.IsEnabled = request.Enabled;
        await db.SaveChangesAsync(ct);
        cache.Set(plugin.AssemblyName, request.Enabled);

        // A disable always takes effect; a hook that fails is logged, since there is nothing left to refuse.
        if (!request.Enabled && running is not null)
            await PluginHooks.RunAsync(running, services, (h, c) => h.OnDisableAsync(c, ct), "OnDisable", logger);
    }
}

public record PluginMenuEntryDto(string PluginId, string Label, string Icon, string Route, string? Permission, int Order);

/// <summary>Enabled plugins' menu contributions for the Angular shell (P5 FR-PLUG-020/U3 FR-SHELL-008).
/// Authenticated only — the frontend applies the same permission filter to these as to the
/// compiled-in menu items (MenuService), so there's one place visibility can't drift.</summary>
public record GetPluginMenuQuery : IRequest<IReadOnlyList<PluginMenuEntryDto>>;

public class GetPluginMenuQueryHandler(ApplicationDbContext db, IReadOnlyList<LoadedPlugin> loadedPlugins)
    : IRequestHandler<GetPluginMenuQuery, IReadOnlyList<PluginMenuEntryDto>>
{
    public async Task<IReadOnlyList<PluginMenuEntryDto>> Handle(GetPluginMenuQuery request, CancellationToken ct)
    {
        var enabledIds = await db.Set<InstalledPlugin>().Where(p => p.IsEnabled).Select(p => p.Id).ToListAsync(ct);
        var enabledSet = enabledIds.ToHashSet();

        return loadedPlugins
            .Where(p => p.Succeeded && enabledSet.Contains(p.Manifest.Id))
            .SelectMany(p => p.Manifest.Menu.Select(m =>
                new PluginMenuEntryDto(p.Manifest.Id, m.Label, m.Icon, m.Route, m.Permission, m.Order)))
            .OrderBy(m => m.Order)
            .ToList();
    }
}
