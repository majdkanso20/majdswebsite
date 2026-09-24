using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

/// <summary>FR-USER-006.</summary>
[RequiresPermission(Permissions.Users.Edit)]
public record UnlockUserCommand(string UserId) : IRequest, IAuditableCommand;

public class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
{
    public UnlockUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class UnlockUserCommandHandler(UserManager<ApplicationUser> userManager) : IRequestHandler<UnlockUserCommand>
{
    public async Task Handle(UnlockUserCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
    }
}
