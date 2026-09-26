using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MajdsApp.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using MajdsApp.Configuration;
using MajdsApp.SharedKernel.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MajdsApp.Services;

public class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpEmailSender> logger, IServiceProvider services) : IEmailSender<ApplicationUser>, MajdsApp.SharedKernel.Notifications.IEmailMessageSender
{
    private readonly SmtpOptions _options = smtpOptions.Value;

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var t = await TranslatorAsync(user.Id);
        await SendEmailAsync(email, t.T("Confirm your email — Majd's App"), t.Body(
            t.T("Welcome to Majd's App!"),
            string.Format(t.T("Please confirm your account by {0}."), $"<a href=\"{confirmationLink}\">{t.T("clicking here")}</a>")));
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var t = await TranslatorAsync(user.Id);
        await SendEmailAsync(email, t.T("Reset your password — Majd's App"), t.Body(
            t.T("You requested a password reset for your Majd's App account."),
            string.Format(t.T("Please reset your password by {0}."), $"<a href=\"{resetLink}\">{t.T("clicking here")}</a>"),
            t.T("If you didn't request this, you can safely ignore this email.")));
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var t = await TranslatorAsync(user.Id);
        await SendEmailAsync(email, t.T("Your password reset code — Majd's App"), t.Body(
            t.T("You requested a password reset for your Majd's App account."),
            string.Format(t.T("Your reset code is: {0}"), $"<strong>{resetCode}</strong>"),
            t.T("If you didn't request this, you can safely ignore this email.")));
    }

    /// <summary>Translates email text into the recipient's saved language (F-Localization FR-I18N-002); English when they have none.</summary>
    private sealed class Translator(IMessageCatalog? catalog, string culture)
    {
        public string T(string text) => catalog?.Translate(text, culture) ?? text;

        public string Body(params string[] paragraphs) =>
            $"<div dir=\"{(culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr")}\">" + string.Concat(paragraphs.Select(p => $"<p>{p}</p>")) + "</div>";
    }

    private async Task<Translator> TranslatorAsync(string userId)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<MajdsApp.SharedKernel.Settings.ISettingsProvider>();
        var culture = (await settings.GetAllForUserAsync(userId)).GetValueOrDefault("General.DefaultLanguage") ?? "en";
        return new Translator(services.GetService<IMessageCatalog>(), culture);
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) =>
        SendEmailAsync(toEmail, subject, htmlBody, ct);

    // The token is passed to every network call so a timeout or shutdown (see ResilientEmailMessageSender) really stops the send.
    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        // Admin-entered settings (Email.*, password stored encrypted) win over the server configuration
        // when non-empty, so credentials can be rotated from the UI without a redeploy.
        // Settings are scoped, but this sender is also resolved from the root provider (Identity's
        // endpoint mapping), so read them through a short-lived scope of our own.
        string host, username, password, fromEmail;
        int port;
        using (var scope = services.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<MajdsApp.SharedKernel.Settings.ISettingsProvider>();
            async Task<string> OrAsync(string name, string fallback)
            {
                var value = await settings.GetAsync(name, ct);
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }

            host = await OrAsync("Email.SmtpHost", _options.Host);
            port = int.TryParse(await settings.GetAsync("Email.SmtpPort", ct), out var p) && p > 0 ? p : _options.Port;
            username = await OrAsync("Email.SmtpUsername", _options.Username);
            password = await OrAsync("Email.SmtpPassword", _options.Password);
            fromEmail = await OrAsync("Email.FromEmail", _options.FromEmail);
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(username, password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Sent email '{Subject}' to {Email}", subject, toEmail);
    }
}
