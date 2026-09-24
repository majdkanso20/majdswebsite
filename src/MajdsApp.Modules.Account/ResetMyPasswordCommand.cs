using System.Text;
using FluentValidation;
using MajdsApp.Data;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace MajdsApp.Modules.Account;

/// <summary>
/// Consumes the link sent by <see cref="ForgotPasswordCommand"/> to actually set a new password.
/// Anonymous by design — the caller proves ownership of the account via the emailed token, not a session.
/// </summary>
public record ResetMyPasswordCommand(string Email, string Code, string NewPassword) : IRequest;

public class ResetMyPasswordCommandValidator : AbstractValidator<ResetMyPasswordCommand>
{
    public ResetMyPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public class ResetMyPasswordCommandHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<ResetMyPasswordCommand>
{
    public async Task Handle(ResetMyPasswordCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new ValidationException("This reset link is invalid or has expired.");

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        }
        catch (FormatException)
        {
            throw new ValidationException("This reset link is invalid or has expired.");
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
