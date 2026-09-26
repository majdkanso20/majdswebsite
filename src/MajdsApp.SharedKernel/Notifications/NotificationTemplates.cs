using MajdsApp.SharedKernel.Localization;

namespace MajdsApp.SharedKernel.Notifications;

/// <summary>The channels a notification can be delivered on (F-Notifications FR-NOTIF-002).</summary>
[Flags]
public enum NotificationChannel { None = 0, InApp = 1, Email = 2, All = InApp | Email }

/// <summary>What a caller wants to say, in the source language (English), before it is turned into one channel's wording.</summary>
public record NotificationContent(string Title, string Message, string? Link = null);

/// <summary>
/// What one channel actually delivers. <see cref="Title"/> and <see cref="Message"/> are plain text; <see cref="HtmlBody"/> is the whole body for a
/// channel that shows markup (email) and is null for one that does not.
/// </summary>
public record RenderedNotification(string Title, string Message, string? HtmlBody = null);

/// <summary>
/// Produces a notification's content from templates, per channel and type, in the recipient's language (F-Notifications FR-NOTIF-003).
/// Callers never build channel text themselves: the dispatcher asks for each channel it will use.
/// </summary>
public interface INotificationTemplateRenderer
{
    RenderedNotification Render(NotificationChannel channel, string type, NotificationContent content, string culture);
}

/// <summary>
/// A template for one notification type on one channel. Register an implementation (in any module) and it is used instead of the default
/// layout for exactly that type and channel; everything else keeps the default. Every piece of text should go through <paramref name="catalog"/>,
/// so it follows the recipient's language, and be HTML-encoded when it goes into markup.
/// </summary>
public interface INotificationTemplate
{
    string Type { get; }
    NotificationChannel Channel { get; }
    RenderedNotification Render(NotificationContent content, string culture, IMessageCatalog catalog);
}
