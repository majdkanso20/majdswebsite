using MailKit;
using MailKit.Net.Smtp;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace MajdsApp.Services;

/// <summary>The mail pipeline, one per application so its circuit breaker is shared by every send.</summary>
public sealed class EmailPipeline(IOptions<OutboundResilienceOptions> options, ILogger<EmailPipeline> logger)
{
    public ResiliencePipeline Pipeline { get; } = OutboundResilience.Create("Email", options.Value, IsTransient, logger);

    /// <summary>A dropped connection, a timeout or a mail server saying "try again later" (a 4xx reply) is worth retrying.
    /// A wrong password, a rejected address or any other 5xx reply is not: retrying only delays the answer and can lock the account.</summary>
    public static bool IsTransient(Exception ex) => ex switch
    {
        SmtpCommandException command => (int)command.StatusCode is >= 400 and < 500,
        MailKit.Security.AuthenticationException => false,
        ServiceNotConnectedException or ServiceNotAuthenticatedException => false,
        ProtocolException => false,
        _ => OutboundResilience.IsCommonTransient(ex)
    };
}

/// <summary>
/// Adds retry, a timeout and a circuit breaker around sending a notification email (P4 FR-XC-004/006). It is a decorator, applied with Scrutor's
/// <c>Decorate</c>, so <see cref="SmtpEmailSender"/> stays a plain sender and any other implementation gets the same protection.
/// </summary>
public sealed class ResilientEmailMessageSender(IEmailMessageSender inner, EmailPipeline pipeline) : IEmailMessageSender
{
    public async Task<DeliveryOutcome> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) =>
        await pipeline.Pipeline.ExecuteAsync(async token => await inner.SendAsync(toEmail, subject, htmlBody, token), ct);
}
