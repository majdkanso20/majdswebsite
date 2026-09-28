namespace MajdsApp.SharedKernel.Notifications;

/// <summary>A message on its way out through one channel: who it is for, the plain text, and (for a channel that shows markup) a whole body.</summary>
public record OutboundMessage(string UserId, string Type, string Title, string Message, string? HtmlBody);

/// <summary>
/// A way of delivering notifications to people outside the application: email, SMS, push, a chat message, anything (F-Notifications FR-NOTIF-002/009).
/// Adding a channel means writing one class that implements this and registering it (<c>services.AddScoped&lt;IOutboundChannel, MyChannel&gt;()</c>) in a module or plugin:
/// the dispatcher queues a message for it, the background worker retries it with backoff, the delivery mode (Dev, Test or Prod) is applied to it,
/// and it gets its own column in each user's notification preferences, with no change to the notification module.
/// </summary>
public interface IOutboundChannel
{
    /// <summary>The channel's short, stable name (<c>Email</c>, <c>Sms</c>). It names the channel in stored deliveries and preferences and in the settings
    /// <c>Notifications.{Name}.DeliveryMode</c> and <c>Notifications.{Name}.TestRecipient</c>.</summary>
    string Name { get; }

    /// <summary>The column heading in a user's notification preferences (translated by the screen).</summary>
    string DisplayName { get; }

    /// <summary>Whether a user who has never chosen receives notifications on this channel. Email is on; a channel that costs money or needs a phone number is best off.</summary>
    bool EnabledByDefault => true;

    /// <summary>True when the channel applies the delivery mode itself (email does, inside the mail sender, so that password-reset mail follows it too).
    /// Otherwise the worker applies it before calling <see cref="SendAsync"/>.</summary>
    bool AppliesDeliveryModeItself => false;

    /// <summary>The address or number to reach this user on (an email address, a phone number, a device id), or null when they have none.</summary>
    Task<string?> ResolveAddressAsync(string userId, CancellationToken ct = default);

    /// <summary>Delivers the message. Throw to have it retried later with backoff; return <see cref="DeliveryOutcome.Suppressed"/> when it was deliberately not sent.</summary>
    Task<DeliveryOutcome> SendAsync(string address, OutboundMessage message, CancellationToken ct = default);
}
