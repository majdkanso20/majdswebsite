using System.Text;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace MajdsApp.Modules.Users;

/// <summary>
/// FR-USER-002: either the admin sets an initial password directly (<paramref name="Password"/>), or, with
/// <paramref name="SendSetPasswordEmail"/>, the account gets a password nobody knows (<see cref="PasswordGenerator"/>)
/// and an email with a set-password link — the same mechanism as Forgot password (<c>ForgotPasswordCommand</c>), since
/// a brand-new account with no password the owner knows is exactly that situation.
/// </summary>
[RequiresPermission(Permissions.Users.Create)]
public record CreateUserCommand(string Email, string? Password, bool SendSetPasswordEmail, string? FullName, IReadOnlyList<string> Roles)
    : IRequest<string>, IAuditableCommand;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).Unless(x => x.SendSetPasswordEmail);
    }
}

public class CreateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IEmailSender<ApplicationUser>? emailSender = null) : IRequestHandler<CreateUserCommand, string>
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

        var password = request.SendSetPasswordEmail ? PasswordGenerator.Generate() : request.Password!;
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (request.Roles.Count > 0)
            await userManager.AddToRolesAsync(user, request.Roles);

        if (request.SendSetPasswordEmail)
            await SendSetPasswordLinkAsync(user, ct);

        return user.Id;
    }

    private async Task SendSetPasswordLinkAsync(ApplicationUser user, CancellationToken ct)
    {
        if (emailSender is null) return; // SMTP not configured in this environment — the account still works, just via Forgot password later

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var spaOrigin = configuration.GetSection("Spa:AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:4200";
        var link = $"{spaOrigin}/reset-password?email={Uri.EscapeDataString(user.Email!)}&code={code}";

        await emailSender.SendPasswordResetLinkAsync(user, user.Email!, link);
    }
}
