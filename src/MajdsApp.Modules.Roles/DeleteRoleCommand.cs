using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

/// <summary>AC-ROLE-1: a static (seeded) role is never deletable. A role with assigned users is rejected, rather than silently orphaning
/// those assignments, unless <paramref name="ReassignToRoleId"/> names the role that takes them over (F-Roles FR-ROLE-004).</summary>
[RequiresPermission(Permissions.Roles.Delete)]
public record DeleteRoleCommand(string RoleId, string? ReassignToRoleId = null) : IRequest, IAuditableCommand, ITransactionalCommand;

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public class DeleteRoleCommandHandler(RoleManager<ApplicationRole> roleManager, ApplicationDbContext db, PermissionChecker permissions) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(request.RoleId)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        if (role.IsStatic)
            throw new ConflictException($"'{role.Name}' is a built-in role and cannot be deleted.");

        var holders = await db.UserRoles.Where(ur => ur.RoleId == role.Id).ToListAsync(ct);
        if (holders.Count > 0)
        {
            if (string.IsNullOrEmpty(request.ReassignToRoleId))
                throw new ConflictException("This role still has users assigned to it. Reassign them before deleting the role.");

            var target = await roleManager.FindByIdAsync(request.ReassignToRoleId)
                ?? throw new NotFoundException($"Role '{request.ReassignToRoleId}' was not found.");
            if (target.Id == role.Id)
                throw new ValidationException("Choose a different role to move the users to.");

            // Each holder keeps access by taking the other role; anyone who already has it just loses this one.
            var alreadyThere = (await db.UserRoles.Where(ur => ur.RoleId == target.Id).Select(ur => ur.UserId).ToListAsync(ct)).ToHashSet();
            foreach (var holder in holders.Where(h => !alreadyThere.Contains(h.UserId)))
                db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = holder.UserId, RoleId = target.Id });
            db.UserRoles.RemoveRange(holders);
            await db.SaveChangesAsync(ct);
            foreach (var holder in holders) permissions.InvalidateUser(holder.UserId);
        }

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
