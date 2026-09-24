using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

/// <summary>AC-ROLE-1: a static (seeded) role is never deletable. A role with assigned users is
/// rejected too, rather than silently orphaning those assignments.</summary>
[RequiresPermission(Permissions.Roles.Delete)]
public record DeleteRoleCommand(string RoleId) : IRequest, IAuditableCommand;

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public class DeleteRoleCommandHandler(RoleManager<ApplicationRole> roleManager, ApplicationDbContext db) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(request.RoleId)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        if (role.IsStatic)
            throw new ConflictException($"'{role.Name}' is a built-in role and cannot be deleted.");

        var hasUsers = await db.UserRoles.AnyAsync(ur => ur.RoleId == role.Id, ct);
        if (hasUsers)
            throw new ConflictException("This role still has users assigned to it. Reassign them before deleting the role.");

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
