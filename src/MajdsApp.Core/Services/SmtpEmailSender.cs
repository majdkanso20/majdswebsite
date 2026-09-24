using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MajdsApp.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using MajdsApp.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MajdsApp.Services;

public class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpEmailSender> logger, IServiceProvider services) : IEmailSender<ApplicationUser>, MajdsApp.SharedKernel.Notifications.IEmailMessageSender
{
    private readonly SmtpOptions _options = smtpOptions.Value;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(email, "Confirm your email — Majd's App",
            $"""
             <p>Welcome to Majd's App!</p>
             <p>Please confirm your account by <a href="{confirmationLink}">clicking here</a>.</p>
             """);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(email, "Reset your password — Majd's App",
            $"""
             <p>You requested a password reset for your Majd's App account.</p>
             <p>Please reset your password by <a href="{resetLink}">clicking here</a>.</p>
             <p>If you didn't request this, you can safely ignore this email.</p>
             """);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendEmailAsync(email, "Your password reset code — Majd's App",
            $"""
             <p>You requested a password reset for your Majd's App account.</p>
             <p>Your reset code is: <strong>{resetCode}</strong></p>
             <p>If you didn't request this, you can safely ignore this email.</p>
             """);

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) =>
        SendEmailAsync(toEmail, subject, htmlBody);

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
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
                var value = await settings.GetAsync(name);
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }

            host = await OrAsync("Email.SmtpHost", _options.Host);
            port = int.TryParse(await settings.GetAsync("Email.SmtpPort"), out var p) && p > 0 ? p : _options.Port;
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
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(username, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("Sent email '{Subject}' to {Email}", subject, toEmail);
    }
}
