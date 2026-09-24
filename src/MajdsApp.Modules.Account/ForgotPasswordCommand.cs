using System.Text;
using FluentValidation;
using MajdsApp.Data;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace MajdsApp.Modules.Account;

/// <summary>
/// Self-service "forgot password" for a local account (no identity provider involved) — the piece the
/// admin-initiated <c>MajdsApp.Modules.Users.ResetPasswordCommand</c> doesn't cover: a user who isn't
/// signed in and has no admin to ask. Anonymous by design; the handler never reveals whether the email
/// exists (same response either way), so it can't be used to enumerate accounts.
/// </summary>
public record ForgotPasswordCommand(string Email) : IRequest;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IEmailSender<ApplicationUser>? emailSender = null) : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        if (emailSender is null)
            return; // SMTP not configured in this environment — same silent no-op as registration's confirmation email

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
            return; // deliberately silent: don't confirm/deny whether the address has an account

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var spaOrigin = configuration.GetSection("Spa:AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:4200";
        var link = $"{spaOrigin}/reset-password?email={Uri.EscapeDataString(request.Email)}&code={code}";

        await emailSender.SendPasswordResetLinkAsync(user, user.Email!, link);
    }
}
