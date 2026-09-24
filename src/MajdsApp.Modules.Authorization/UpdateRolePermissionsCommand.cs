using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Authorization;

/// <summary>Replaces a role's permission set (FR-AUTHZ-004 guard via [RequiresPermission], AC-AUTHZ-1/2).</summary>
[RequiresPermission(Permissions.Roles.Edit)]
public record UpdateRolePermissionsCommand(string RoleId, IReadOnlyList<string> PermissionNames) : IRequest, ITransactionalCommand, IAuditableCommand;

public class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public class UpdateRolePermissionsCommandHandler(ApplicationDbContext db, PermissionChecker permissionChecker)
    : IRequestHandler<UpdateRolePermissionsCommand>
{
    public async Task Handle(UpdateRolePermissionsCommand request, CancellationToken ct)
    {
        var existing = await db.Set<RolePermission>().Where(rp => rp.RoleId == request.RoleId).ToListAsync(ct);
        db.Set<RolePermission>().RemoveRange(existing);

        var allowedNames = new HashSet<string>(PermissionRegistry.GetAllPermissionNames());
        foreach (var name in request.PermissionNames.Distinct().Where(allowedNames.Contains))
        {
            db.Set<RolePermission>().Add(new RolePermission { RoleId = request.RoleId, PermissionName = name, IsGranted = true });
        }

        await permissionChecker.InvalidateRoleAsync(request.RoleId, ct);
    }
}
