using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

/// <summary>FR-USER-005: soft delete only. AC via SuperAdminGuard (FR-USER-007).</summary>
[RequiresPermission(Permissions.Users.Delete)]
public record DeleteUserCommand(string UserId) : IRequest, IAuditableCommand;

public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class DeleteUserCommandHandler(UserManager<ApplicationUser> userManager, SuperAdminGuard superAdminGuard)
    : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        superAdminGuard.EnsureNotSelf(request.UserId);
        await superAdminGuard.EnsureNotLastSuperAdminAsync(user, ct);

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.UpdateSecurityStampAsync(user);
    }
}
