using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Roles;

/// <summary>A static role's Name can't change (FR-ROLE-003); DisplayName/IsDefault always can.</summary>
[RequiresPermission(Permissions.Roles.Edit)]
public record UpdateRoleCommand(string RoleId, string? DisplayName, bool IsDefault) : IRequest, IAuditableCommand;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public class UpdateRoleCommandHandler(RoleManager<ApplicationRole> roleManager) : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(request.RoleId)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        if (request.IsDefault && !role.IsDefault)
        {
            foreach (var existingDefault in roleManager.Roles.Where(r => r.IsDefault && r.Id != role.Id).ToList())
            {
                existingDefault.IsDefault = false;
                await roleManager.UpdateAsync(existingDefault);
            }
        }

        role.DisplayName = request.DisplayName;
        role.IsDefault = request.IsDefault;

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
