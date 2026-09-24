using System.Text;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace MajdsApp.Modules.Users;

/// <summary>
/// FR-USER-006: admin-initiated password reset. Reuses the existing self-service reset flow (the
/// Razor Pages ResetPassword page, AS-4's "existing Authentication") rather than a new one — the
/// admin just triggers the same email a forgotten-password request would send.
/// </summary>
[RequiresPermission(Permissions.Users.Edit)]
public record ResetPasswordCommand(string UserId) : IRequest, IAuditableCommand;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ResetPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    IConfiguration configuration) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var webAppBaseUrl = configuration["WebApp:BaseUrl"] ?? "http://localhost:5132";
        var resetLink = $"{webAppBaseUrl}/Identity/Account/ResetPassword?code={code}";

        await emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);
    }
}
