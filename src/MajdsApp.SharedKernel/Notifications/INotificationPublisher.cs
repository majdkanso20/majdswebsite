namespace MajdsApp.SharedKernel.Notifications;

/// <summary>Well-known notification types users can subscribe to per channel (F-Notifications FR-NOTIF-006).</summary>
public static class NotificationTypes
{
    public const string General = "General";
    public const string Security = "Security";
    public const string Account = "Account";
    public const string Administration = "Administration";

    public static readonly IReadOnlyList<string> All = [General, Security, Account, Administration];
}

/// <summary>How serious a notification is (F-Notifications FR-NOTIF-004): a hint for the UI to style or sort by, not a delivery rule.</summary>
public enum NotificationSeverity { Info = 0, Success = 1, Warning = 2, Error = 3 }

/// <summary>Lets any module notify users without depending on F-Notifications. The Shared Kernel ships
/// a no-op default; the F-Notifications module replaces it (same pattern as <see cref="Security.IPermissionChecker"/>).
/// The module routes each notification to the channels the recipient enabled for its <c>type</c>.</summary>
public interface IUserNotificationPublisher
{
    /// <param name="link">An in-app route the notification opens when clicked (for example <c>/exports</c>), or null.</param>
    /// <param name="payload">Small, arbitrary JSON a client can read back (for example the export's id), stored with the notification (FR-NOTIF-004); null for none.</param>
    Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default, string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null);
    Task PublishToAllAsync(string title, string message, string type = NotificationTypes.General, CancellationToken ct = default,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null);
    Task PublishToRoleAsync(string roleName, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null);
}

public class NullNotificationPublisher : IUserNotificationPublisher
{
    public Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default, string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null) => Task.CompletedTask;
    public Task PublishToAllAsync(string title, string message, string type = NotificationTypes.General, CancellationToken ct = default,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null) => Task.CompletedTask;
    public Task PublishToRoleAsync(string roleName, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null) => Task.CompletedTask;
}

/// <summary>What to send (F-Notifications FR-NOTIF-001): type, severity and an optional link/payload, independent of who receives it.</summary>
public record NotificationMessage(string Title, string Message, string Type = NotificationTypes.General,
    NotificationSeverity Severity = NotificationSeverity.Info, string? Link = null, string? Payload = null);

/// <summary>
/// The single dispatch entry point the SRS names (FR-NOTIF-001): one call routes a <see cref="NotificationMessage"/> to whichever
/// channels each recipient enabled for its type. <see cref="IUserNotificationPublisher"/> is the convenience wrapper most callers use
/// (a title and a message, no record to build); both are implemented by the same class in the F-Notifications module.
/// </summary>
public interface INotificationDispatcher
{
    Task SendAsync(NotificationMessage notification, IReadOnlyList<string> recipientUserIds, CancellationToken ct = default);
}

/// <summary>Sends a plain HTML email to an address (used by the email notification channel). Registered by the
/// host only when SMTP is configured; the email channel is skipped when it is absent.</summary>
public interface IEmailMessageSender
{
    /// <summary>Hands a message to the mail channel. What actually happens depends on the delivery mode (see <see cref="DeliveryMode"/>): the result says whether
    /// it was sent, sent to the test recipient instead, or only logged.</summary>
    Task<DeliveryOutcome> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
