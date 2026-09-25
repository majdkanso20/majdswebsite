using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Plugins;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Plugins;

public record PluginDto(
    string Id, string Name, string Version, string Author, bool IsEnabled, string? LastError, DateTime DiscoveredAt,
    IReadOnlyList<string> Permissions, IReadOnlyList<string> MenuEntries, bool CanRollback, bool PendingUninstall,
    string? MinHostVersion, string? MaxHostVersion);

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
                plugin?.Manifest.MinHostVersion, plugin?.Manifest.MaxHostVersion);
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

public class SetPluginEnabledCommandHandler(ApplicationDbContext db, PluginStateCache cache)
    : IRequestHandler<SetPluginEnabledCommand>
{
    public async Task Handle(SetPluginEnabledCommand request, CancellationToken ct)
    {
        var plugin = await db.Set<InstalledPlugin>().FirstOrDefaultAsync(p => p.Id == request.PluginId, ct)
            ?? throw new NotFoundException($"Plugin '{request.PluginId}' was not found.");

        plugin.IsEnabled = request.Enabled;
        await db.SaveChangesAsync(ct);

        cache.Set(plugin.AssemblyName, request.Enabled);
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
