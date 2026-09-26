using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Plugins;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Plugins;

public record PluginChangeDto(string Id, string Name, string Version, string Action, string? PreviousVersion, string Sha256, bool RestartRequired, string? SignedBy = null);

public record PendingPluginChangeDto(string Id, string Name, string Version, string Action, DateTime StagedAt);

/// <summary>Verifies an uploaded plugin package (.zip) and stages it to be installed, or to upgrade an installed plugin, at the
/// next start (P5 FR-PLUG-032/036/038/040). Nothing that is running is touched; the checks and the trust policy are in
/// <see cref="PluginInstaller"/>. The result carries the package's SHA-256 so it can be compared with the publisher's.</summary>
[RequiresPermission(Permissions.Plugins.Manage)]
public record InstallPluginCommand(Stream Content, string FileName, string? ExpectedSha256) : IRequest<PluginChangeDto>, IAuditableCommand;

public class InstallPluginCommandHandler(PluginHostOptions options) : IRequestHandler<InstallPluginCommand, PluginChangeDto>
{
    public Task<PluginChangeDto> Handle(InstallPluginCommand request, CancellationToken ct)
    {
        var staged = PluginInstaller.Stage(request.Content, request.ExpectedSha256, options);
        return Task.FromResult(new PluginChangeDto(
            staged.Manifest.Id, staged.Manifest.Name, staged.Manifest.Version, staged.Action, staged.PreviousVersion, staged.Sha256, RestartRequired: true, staged.SignedBy));
    }
}

/// <summary>Stages the version that was installed before the last change, to be restored at the next start (P5 FR-PLUG-038).</summary>
[RequiresPermission(Permissions.Plugins.Manage)]
public record RollbackPluginCommand(string PluginId) : IRequest<PluginChangeDto>, IAuditableCommand;

public class RollbackPluginCommandValidator : AbstractValidator<RollbackPluginCommand>
{
    public RollbackPluginCommandValidator() => RuleFor(x => x.PluginId).NotEmpty();
}

public class RollbackPluginCommandHandler(PluginHostOptions options) : IRequestHandler<RollbackPluginCommand, PluginChangeDto>
{
    public Task<PluginChangeDto> Handle(RollbackPluginCommand request, CancellationToken ct)
    {
        var staged = PluginInstaller.StageRollback(request.PluginId, options);
        return Task.FromResult(new PluginChangeDto(
            staged.Manifest.Id, staged.Manifest.Name, staged.Manifest.Version, staged.Action, staged.PreviousVersion, staged.Sha256, RestartRequired: true, staged.SignedBy));
    }
}

/// <summary>
/// Uninstalls a plugin (P5 FR-PLUG-028/034). It stops working straight away (the enabled-state gate blocks its API and its menu
/// entries disappear), its permissions are removed from every role and user that held them so nothing is orphaned, and its
/// registry entry is removed. Its files cannot be deleted while it is loaded, so they are removed at the next start.
/// Its data is always kept: deleting a plugin's tables needs an explicit, reviewed migration and is not offered here.
/// </summary>
[RequiresPermission(Permissions.Plugins.Manage)]
public record UninstallPluginCommand(string PluginId) : IRequest, IAuditableCommand, ITransactionalCommand;

public class UninstallPluginCommandValidator : AbstractValidator<UninstallPluginCommand>
{
    public UninstallPluginCommandValidator() => RuleFor(x => x.PluginId).NotEmpty();
}

public class UninstallPluginCommandHandler(
    ApplicationDbContext db, PluginStateCache cache, IReadOnlyList<LoadedPlugin> loaded, PluginHostOptions options, PermissionChecker permissions,
    IServiceProvider services, ILogger<UninstallPluginCommandHandler> logger)
    : IRequestHandler<UninstallPluginCommand>
{
    public async Task Handle(UninstallPluginCommand request, CancellationToken ct)
    {
        var plugin = await db.Set<InstalledPlugin>().FirstOrDefaultAsync(p => p.Id == request.PluginId, ct)
            ?? throw new NotFoundException($"Plugin '{request.PluginId}' was not found.");

        var dependents = loaded.Where(l => l.Succeeded && l.Manifest.Id != plugin.Id && l.Manifest.DependsOn.Any(d => d.Id == plugin.Id)).Select(l => l.Manifest.Name).ToList();
        var dependentNames = string.Join(", ", dependents);
        if (dependents.Count > 0)
            throw new ConflictException($"'{plugin.Name}' is needed by {dependentNames}; uninstall those first.");

        PluginInstaller.MarkForUninstall(plugin.Id, options); // throws if there is nothing on disk, before anything else changes
        try
        {
            var declared = loaded.FirstOrDefault(l => l.Manifest.Id == plugin.Id)?.Assembly is { } assembly
                ? PermissionRegistry.GetPermissionNames(assembly)
                : [];

            if (declared.Count > 0)
            {
                var roleGrants = await db.Set<RolePermission>().Where(rp => declared.Contains(rp.PermissionName)).ToListAsync(ct);
                var userGrants = await db.Set<UserPermission>().Where(up => declared.Contains(up.PermissionName)).ToListAsync(ct);
                var roleIds = roleGrants.Select(g => g.RoleId).Distinct().ToList();
                var userIds = userGrants.Select(g => g.UserId).Distinct().ToList();

                db.Set<RolePermission>().RemoveRange(roleGrants);
                db.Set<UserPermission>().RemoveRange(userGrants);
                db.Set<InstalledPlugin>().Remove(plugin);
                await db.SaveChangesAsync(ct);

                foreach (var roleId in roleIds) await permissions.InvalidateRoleAsync(roleId, ct);
                foreach (var userId in userIds) permissions.InvalidateUser(userId);
            }
            else
            {
                db.Set<InstalledPlugin>().Remove(plugin);
                await db.SaveChangesAsync(ct);
            }

            cache.Set(plugin.AssemblyName, false); // blocks its API for the rest of this run
        }
        catch
        {
            PluginInstaller.CancelPending(plugin.Id, options); // the database change failed: do not leave a marker behind
            throw;
        }

        // FR-PLUG-031: the plugin's own clean-up. The uninstall has happened, so a failure here is logged, not raised.
        if (loaded.FirstOrDefault(l => l.Manifest.Id == plugin.Id) is { } running)
            await PluginHooks.RunAsync(running, services, (h, c) => h.OnUninstallAsync(c, ct), "OnUninstall", logger);
    }
}

/// <summary>Drops a staged install, upgrade or rollback, or an uninstall marker, before the next start applies it.</summary>
[RequiresPermission(Permissions.Plugins.Manage)]
public record CancelPluginChangeCommand(string PluginId) : IRequest, IAuditableCommand;

public class CancelPluginChangeCommandValidator : AbstractValidator<CancelPluginChangeCommand>
{
    public CancelPluginChangeCommandValidator() => RuleFor(x => x.PluginId).NotEmpty();
}

public class CancelPluginChangeCommandHandler(PluginHostOptions options) : IRequestHandler<CancelPluginChangeCommand>
{
    public Task Handle(CancelPluginChangeCommand request, CancellationToken ct) =>
        PluginInstaller.CancelPending(request.PluginId, options)
            ? Task.CompletedTask
            : throw new NotFoundException($"There is no pending change for '{request.PluginId}'.");
}

[RequiresPermission(Permissions.Plugins.View)]
public record ListPendingPluginChangesQuery : IRequest<IReadOnlyList<PendingPluginChangeDto>>;

public class ListPendingPluginChangesQueryHandler(PluginHostOptions options)
    : IRequestHandler<ListPendingPluginChangesQuery, IReadOnlyList<PendingPluginChangeDto>>
{
    public Task<IReadOnlyList<PendingPluginChangeDto>> Handle(ListPendingPluginChangesQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PendingPluginChangeDto>>(PluginInstaller.ListPending(options.Directory)
            .Select(c => new PendingPluginChangeDto(c.Id, c.Name, c.Version, c.Action, c.StagedAt)).ToList());
}
