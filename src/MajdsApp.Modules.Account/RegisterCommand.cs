using System.Text;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MajdsApp.Modules.Account;

/// <summary>
/// Public self-registration, gated by the "Allow users to self-register" setting. The account starts
/// unconfirmed and can't sign in until the emailed link is used (same confirmation page as the Razor
/// site). An address that's already registered gets the same "check your email" answer, so the form
/// can't be used to find out who has an account.
/// </summary>
public record RegisterCommand(string Email, string Password, string? FullName) : IRequest, IAuditableCommand;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FullName).MaximumLength(200);
    }
}

public class RegisterCommandHandler(
    UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles,
    ISettingsProvider settings,
    IUserNotificationPublisher notifications,
    IConfiguration configuration,
    IEmailSender<ApplicationUser>? emailSender = null) : IRequestHandler<RegisterCommand>
{
    public async Task Handle(RegisterCommand request, CancellationToken ct)
    {
        if (!await settings.GetBooleanAsync("Security.AllowSelfRegistration", ct))
            throw new ForbiddenException("New account registration is turned off.");

        if (await users.FindByEmailAsync(request.Email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim()
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new ValidationException(string.Join("; ", created.Errors.Select(e => e.Description)));

        var defaultRoles = await roles.Roles.Where(r => r.IsDefault).Select(r => r.Name!).ToListAsync(ct);
        if (defaultRoles.Count > 0)
            await users.AddToRolesAsync(user, defaultRoles);

        if (emailSender is not null)
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = configuration["WebApp:BaseUrl"] ?? "http://localhost:5132";
            await emailSender.SendConfirmationLinkAsync(user, user.Email!,
                $"{baseUrl}/Identity/Account/ConfirmEmail?userId={user.Id}&code={code}");
        }

        await notifications.PublishToRoleAsync("Admin", "New user signed up", $"{request.Email} registered.", NotificationTypes.Administration, ct);
    }
}
