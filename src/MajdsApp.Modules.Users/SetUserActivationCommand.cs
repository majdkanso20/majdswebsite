using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

/// <summary>FR-USER-004, AC-USER-2/3.</summary>
[RequiresPermission(Permissions.Users.Edit)]
public record SetUserActivationCommand(string UserId, bool IsActive) : IRequest, IAuditableCommand;

public class SetUserActivationCommandValidator : AbstractValidator<SetUserActivationCommand>
{
    public SetUserActivationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class SetUserActivationCommandHandler(UserManager<ApplicationUser> userManager, SuperAdminGuard superAdminGuard)
    : IRequestHandler<SetUserActivationCommand>
{
    public async Task Handle(SetUserActivationCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        if (!request.IsActive)
        {
            superAdminGuard.EnsureNotSelf(request.UserId);
            await superAdminGuard.EnsureNotLastSuperAdminAsync(user, ct);
        }

        user.IsActive = request.IsActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (!request.IsActive)
            await userManager.UpdateSecurityStampAsync(user); // Invalidates any outstanding bearer/refresh tokens immediately.
    }
}
