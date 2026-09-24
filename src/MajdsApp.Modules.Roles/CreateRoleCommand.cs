using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Roles;

[RequiresPermission(Permissions.Roles.Create)]
public record CreateRoleCommand(string Name, string? DisplayName, bool IsDefault) : IRequest<string>, IAuditableCommand;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
    }
}

public class CreateRoleCommandHandler(RoleManager<ApplicationRole> roleManager) : IRequestHandler<CreateRoleCommand, string>
{
    public async Task<string> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        if (await roleManager.FindByNameAsync(request.Name) is not null)
            throw new ConflictException($"A role named '{request.Name}' already exists.");

        // Only one default role makes sense — a new user is auto-assigned all default roles (AC-ROLE-2).
        if (request.IsDefault)
        {
            foreach (var existingDefault in roleManager.Roles.Where(r => r.IsDefault).ToList())
            {
                existingDefault.IsDefault = false;
                await roleManager.UpdateAsync(existingDefault);
            }
        }

        var role = new ApplicationRole
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            IsStatic = false,
            IsDefault = request.IsDefault
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return role.Id;
    }
}
