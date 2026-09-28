using MajdsApp.SharedKernel.Mapping;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// The notification dispatcher (FR-NOTIF-001): for each recipient it reads their per-type channel
/// choices, writes and pushes the in-app notification in real time, and queues email for the
/// background worker. <see cref="SendAsync"/> is the single entry point the SRS names; <see cref="IUserNotificationPublisher"/>
/// (implemented on the same class) is the convenience wrapper most callers use instead of building a <see cref="NotificationMessage"/> by hand.
/// </summary>
public class NotificationPublisher(
    ApplicationDbContext db, IHubContext<NotificationHub> hub, ISettingsProvider settings, IObjectMapper mapper,
    INotificationTemplateRenderer templates, IEnumerable<IOutboundChannel> outbound) : IUserNotificationPublisher, INotificationDispatcher
{
    public Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default, string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null) =>
        SendAsync(new NotificationMessage(title, message, type, severity, link, payload), [userId], ct);

    public async Task PublishToAllAsync(string title, string message, string type = NotificationTypes.General, CancellationToken ct = default,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null)
    {
        var ids = await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
        await SendAsync(new NotificationMessage(title, message, type, severity, Payload: payload), ids, ct);
    }

    public async Task PublishToRoleAsync(string roleName, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default,
        NotificationSeverity severity = NotificationSeverity.Info, string? payload = null)
    {
        var ids = await (from ur in db.UserRoles
                         join r in db.Roles on ur.RoleId equals r.Id
                         join u in db.Users on ur.UserId equals u.Id
                         where r.Name == roleName && u.IsActive
                         select u.Id).Distinct().ToListAsync(ct);
        await SendAsync(new NotificationMessage(title, message, type, severity, Payload: payload), ids, ct);
    }

    public async Task SendAsync(NotificationMessage notification, IReadOnlyList<string> recipientUserIds, CancellationToken ct = default)
    {
        var (title, message, type, severity, link, payload) = notification;
        var userIds = recipientUserIds;
        if (userIds.Count == 0) return;

        var choices = await db.Set<NotificationSubscription>().AsNoTracking()
            .Where(s => s.Type == type && userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => s.Channels, ct);

        // Choices for the channels modules have added (SMS, push...): one row where a user's choice differs from the channel's default.
        var moduleChannels = outbound.Where(c => c.Name != EmailOutboundChannel.ChannelName).ToList();
        var extraChoices = moduleChannels.Count == 0
            ? new Dictionary<(string User, string Channel), bool>()
            : (await db.Set<NotificationChannelChoice>().AsNoTracking().Where(c => c.Type == type && userIds.Contains(c.UserId)).ToListAsync(ct))
                .ToDictionary(c => (c.UserId, c.ChannelName), c => c.Enabled);

        var now = DateTime.UtcNow;
        var inApp = new List<Notification>();

        foreach (var userId in userIds)
        {
            var channels = choices.TryGetValue(userId, out var c) ? c : NotificationChannel.All;

            // Each recipient gets the text in their own language (F-Localization FR-I18N-002): their saved language, else the
            // application default. Text with no translation (for example one an administrator wrote by hand) is left as written.
            var culture = (await settings.GetAllForUserAsync(userId, ct)).GetValueOrDefault("General.DefaultLanguage") ?? "en";
            // Each channel's wording comes from the template for that type and channel, in the recipient's language (FR-NOTIF-003).
            var content = new NotificationContent(title, message, link);

            if (channels.HasFlag(NotificationChannel.InApp))
            {
                var rendered = templates.Render(NotificationChannel.InApp, type, content, culture);
                inApp.Add(new Notification { UserId = userId, Type = type, Severity = severity, Title = rendered.Title, Message = rendered.Message, Link = link, Payload = payload, CreatedAt = now });
            }

            if (channels.HasFlag(NotificationChannel.Email))
            {
                var rendered = templates.Render(NotificationChannel.Email, type, content, culture);
                db.Set<NotificationDelivery>().Add(new NotificationDelivery
                {
                    UserId = userId, Channel = NotificationChannel.Email, ChannelName = EmailOutboundChannel.ChannelName, Type = type, Severity = severity, Title = rendered.Title, Message = rendered.Message, Body = rendered.HtmlBody, Payload = payload,
                    Status = DeliveryStatus.Pending, CreatedAt = now, NextAttemptAt = now
                });
            }

            // Every other channel a module registered gets the plain text (translated for this recipient) if the user wants it on that channel.
            foreach (var channel in moduleChannels)
            {
                var wanted = extraChoices.TryGetValue((userId, channel.Name), out var enabled) ? enabled : channel.EnabledByDefault;
                if (!wanted) continue;

                var rendered = templates.Render(NotificationChannel.InApp, type, content, culture);
                db.Set<NotificationDelivery>().Add(new NotificationDelivery
                {
                    UserId = userId, Channel = NotificationChannel.None, ChannelName = channel.Name, Type = type, Severity = severity, Title = rendered.Title, Message = rendered.Message, Payload = payload,
                    Status = DeliveryStatus.Pending, CreatedAt = now, NextAttemptAt = now
                });
            }
        }

        db.Set<Notification>().AddRange(inApp);
        await db.SaveChangesAsync(ct);

        foreach (var n in inApp)
            await hub.Clients.User(n.UserId).SendAsync("notification",
                mapper.Map<NotificationDto>(n), ct);
    }
}
