using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Notifications;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Modules.Users;

/// <summary>Switches off a user's two-factor authentication and discards their authenticator key, for
/// someone who lost their phone. They sign in with their password and can set 2FA up again.</summary>
[RequiresPermission(Permissions.Users.Edit)]
public record ResetTwoFactorCommand(string UserId) : IRequest, IAuditableCommand;

public class ResetTwoFactorCommandValidator : AbstractValidator<ResetTwoFactorCommand>
{
    public ResetTwoFactorCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ResetTwoFactorCommandHandler(UserManager<ApplicationUser> userManager, IUserNotificationPublisher notifications)
    : IRequestHandler<ResetTwoFactorCommand>
{
    public async Task Handle(ResetTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0); // invalidates any remaining codes

        await notifications.PublishAsync(user.Id, "Two-factor authentication was reset",
            "An administrator turned off two-factor authentication on your account. You can set it up again under My account.", MajdsApp.SharedKernel.Notifications.NotificationTypes.Security, ct);
    }
}
