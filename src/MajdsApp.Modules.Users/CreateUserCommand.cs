using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

/// <summary>FR-USER-002. The admin sets an initial password directly (simpler than the emailed
/// set-password-link variant the spec also allows — can be added later without changing this contract).</summary>
[RequiresPermission(Permissions.Users.Create)]
public record CreateUserCommand(string Email, string Password, string? FullName, IReadOnlyList<string> Roles) : IRequest<string>, IAuditableCommand;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class CreateUserCommandHandler(UserManager<ApplicationUser> userManager) : IRequestHandler<CreateUserCommand, string>
{
    public async Task<string> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true, // Admin-created accounts are trusted immediately (self-service registration still requires confirmation).
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (request.Roles.Count > 0)
            await userManager.AddToRolesAsync(user, request.Roles);

        return user.Id;
    }
}
