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

/// <summary>Lets any module notify users without depending on F-Notifications. The Shared Kernel ships
/// a no-op default; the F-Notifications module replaces it (same pattern as <see cref="Security.IPermissionChecker"/>).
/// The module routes each notification to the channels the recipient enabled for its <c>type</c>.</summary>
public interface IUserNotificationPublisher
{
    /// <param name="link">An in-app route the notification opens when clicked (for example <c>/exports</c>), or null.</param>
    Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default, string? link = null);
    Task PublishToAllAsync(string title, string message, string type = NotificationTypes.General, CancellationToken ct = default);
    Task PublishToRoleAsync(string roleName, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default);
}

public class NullNotificationPublisher : IUserNotificationPublisher
{
    public Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default, string? link = null) => Task.CompletedTask;
    public Task PublishToAllAsync(string title, string message, string type = NotificationTypes.General, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishToRoleAsync(string roleName, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Sends a plain HTML email to an address (used by the email notification channel). Registered by the
/// host only when SMTP is configured; the email channel is skipped when it is absent.</summary>
public interface IEmailMessageSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
