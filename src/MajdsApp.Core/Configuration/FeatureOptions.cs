using System.ComponentModel.DataAnnotations;

namespace MajdsApp.Configuration;

public class FeatureOptions
{
    public bool TwoFactorAuthenticationEnabled { get; set; } = true;
}

/// <summary>The mail server (<c>Email:Smtp</c>). Checked when the application starts (P2 FR-MOD-004).</summary>
public class SmtpOptions : IValidatableObject
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Email:Smtp:Host must not be empty.")]
    public string Host { get; set; } = "smtp.gmail.com";

    [Range(1, 65535, ErrorMessage = "Email:Smtp:Port must be between 1 and 65535.")]
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Majd's App";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FromEmail.Length > 0 && !new EmailAddressAttribute().IsValid(FromEmail))
            yield return new ValidationResult("Email:Smtp:FromEmail is not a valid email address.", [nameof(FromEmail)]);
    }
}
