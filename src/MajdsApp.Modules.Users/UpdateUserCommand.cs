using MajdsApp.SharedKernel.Notifications;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

[RequiresPermission(Permissions.Users.Edit)]
public record UpdateUserCommand(string UserId, string? FullName, string? PhoneNumber, IReadOnlyList<string> Roles) : IRequest, IAuditableCommand;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class UpdateUserCommandHandler(
    UserManager<ApplicationUser> userManager, IUserNotificationPublisher notifications,
    SuperAdminGuard superAdminGuard, PermissionChecker permissionChecker)
    : IRequestHandler<UpdateUserCommand>
{
    public async Task Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var currentRoles = await userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Except(request.Roles).ToList();
        var toAdd = request.Roles.Except(currentRoles).ToList();

        // FR-USER-007: the last administrator can't be demoted by editing the user either, only by delete/deactivate.
        if (toRemove.Contains(RoleSeeder.AdminRoleName))
            await superAdminGuard.EnsureNotLastSuperAdminAsync(user, ct);

        if (toRemove.Count > 0)
            await userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0)
            await userManager.AddToRolesAsync(user, toAdd);

        if (toRemove.Count > 0 || toAdd.Count > 0)
            permissionChecker.InvalidateUser(user.Id); // FR-AUTHZ-006: role changes must show on the user's very next request

        if (toRemove.Count > 0 || toAdd.Count > 0)
            await notifications.PublishAsync(user.Id, "Your access changed", "An administrator updated your roles.", NotificationTypes.Account, ct);
    }
}
