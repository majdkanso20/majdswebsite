using System.Net;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Notifications;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// The default renderer (FR-NOTIF-003). A template registered for the exact type and channel wins; otherwise the text is translated into the recipient's
/// language and, for email, laid out in a small HTML document whose direction follows the language (right to left for Arabic) so the message reads correctly
/// in the mail client. All text is translated through the message catalog, and HTML-encoded before it goes into markup.
/// </summary>
public class NotificationTemplateRenderer(IMessageCatalog catalog, IEnumerable<INotificationTemplate> templates) : INotificationTemplateRenderer
{
    public RenderedNotification Render(NotificationChannel channel, string type, NotificationContent content, string culture)
    {
        var custom = templates.FirstOrDefault(t => t.Channel == channel && t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        if (custom is not null) return custom.Render(content, culture, catalog);

        var title = catalog.Translate(content.Title, culture);
        var message = catalog.Translate(content.Message, culture);

        return channel == NotificationChannel.Email
            ? new RenderedNotification(title, message, EmailLayout(title, message, footer: null, culture))
            : new RenderedNotification(title, message);
    }

    /// <summary>The default email layout, reused by type-specific templates so they only add what is different.</summary>
    public static string EmailLayout(string title, string message, string? footer, string culture)
    {
        var rtl = culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var footerHtml = footer is null ? "" : $"<p style=\"color:#666;font-size:12px\">{WebUtility.HtmlEncode(footer)}</p>";
        return $"<div dir=\"{(rtl ? "rtl" : "ltr")}\" style=\"font-family:sans-serif\"><h2>{WebUtility.HtmlEncode(title)}</h2><p>{WebUtility.HtmlEncode(message)}</p>{footerHtml}</div>";
    }
}

/// <summary>Security notifications sent by email carry a line telling the reader what to do if it was not them.</summary>
public class SecurityEmailTemplate : INotificationTemplate
{
    public string Type => NotificationTypes.Security;
    public NotificationChannel Channel => NotificationChannel.Email;

    public RenderedNotification Render(NotificationContent content, string culture, IMessageCatalog catalog)
    {
        var title = catalog.Translate(content.Title, culture);
        var message = catalog.Translate(content.Message, culture);
        var footer = catalog.Translate("If this was not you, contact your administrator at once.", culture);
        return new RenderedNotification(title, message, NotificationTemplateRenderer.EmailLayout(title, message, footer, culture));
    }
}
