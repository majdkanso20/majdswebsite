using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.SharedKernel.Notifications;

/// <summary>
/// The environment a notification channel is running in (an administrator sets it; see <see cref="SettingsDeliveryModePolicy"/>):
/// <b>Dev</b> sends nothing and only records what would have been sent, <b>Test</b> really sends but always to a test recipient,
/// and <b>Prod</b> sends to the real recipient.
/// </summary>
public enum DeliveryMode { Dev, Test, Prod }

/// <summary>What happened to a message handed to a channel. <see cref="Redirected"/> means it was sent, but to the test recipient.</summary>
public enum DeliveryOutcome { Sent, Redirected, Suppressed }

/// <summary>Where a message should go, decided for one channel and one intended recipient. <see cref="Recipient"/> is null when nothing is to be sent.</summary>
public record DeliveryDecision(DeliveryMode Mode, string? Recipient, string? SubjectPrefix, string? Reason)
{
    public DeliveryOutcome Outcome(string realRecipient) =>
        Recipient is null ? DeliveryOutcome.Suppressed
        : string.Equals(Recipient, realRecipient, StringComparison.OrdinalIgnoreCase) ? DeliveryOutcome.Sent
        : DeliveryOutcome.Redirected;
}

/// <summary>
/// Decides, for any notification channel (email today, SMS or others as modules add them), whether a message is really sent, redirected to a test recipient,
/// or only logged. A channel asks this just before it sends, so nothing that goes through it can bypass the rule (F-Notifications environment modes).
/// </summary>
public interface IDeliveryModePolicy
{
    /// <param name="channel">The channel's name, for example <c>Email</c> or <c>Sms</c>. It picks the channel's own settings.</param>
    /// <param name="realRecipient">The address or number the message was meant for.</param>
    Task<DeliveryDecision> DecideAsync(string channel, string realRecipient, CancellationToken ct = default);
}

/// <summary>
/// Reads the mode from settings: <c>Notifications.{channel}.DeliveryMode</c> when the channel has its own, else the general <c>Notifications.DeliveryMode</c>
/// (default Prod, so an existing installation keeps sending). Test mode sends to <c>Notifications.{channel}.TestRecipient</c>; without one, or with a mode
/// nobody recognises, nothing is sent: a missing or wrong setting can only ever make the system quieter, never message a real person by mistake.
/// </summary>
public class SettingsDeliveryModePolicy(ISettingsProvider settings) : IDeliveryModePolicy
{
    public async Task<DeliveryDecision> DecideAsync(string channel, string realRecipient, CancellationToken ct = default)
    {
        var configured = await settings.GetAsync($"Notifications.{channel}.DeliveryMode", ct);
        if (string.IsNullOrWhiteSpace(configured)) configured = await settings.GetAsync("Notifications.DeliveryMode", ct);
        if (string.IsNullOrWhiteSpace(configured)) configured = nameof(DeliveryMode.Prod);

        if (!Enum.TryParse<DeliveryMode>(configured, ignoreCase: true, out var mode))
            return new DeliveryDecision(DeliveryMode.Dev, null, null, $"the delivery mode '{configured}' is not Dev, Test or Prod");

        switch (mode)
        {
            case DeliveryMode.Prod:
                return new DeliveryDecision(mode, realRecipient, null, null);

            case DeliveryMode.Test:
                var testRecipient = (await settings.GetAsync($"Notifications.{channel}.TestRecipient", ct)).Trim();
                return testRecipient.Length == 0
                    ? new DeliveryDecision(mode, null, null, "Test mode has no test recipient set")
                    : new DeliveryDecision(mode, testRecipient, $"[Test for {realRecipient}] ", null);

            default:
                return new DeliveryDecision(mode, null, null, "Dev mode sends nothing");
        }
    }
}
