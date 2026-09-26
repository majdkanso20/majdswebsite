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
/// background worker. Callers depend only on <see cref="IUserNotificationPublisher"/>.
/// </summary>
public class NotificationPublisher(
    ApplicationDbContext db, IHubContext<NotificationHub> hub, IMessageCatalog catalog, ISettingsProvider settings, IObjectMapper mapper) : IUserNotificationPublisher
{
    public Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default, string? link = null) =>
        DispatchAsync([userId], title, message, type, ct, link);

    public async Task PublishToAllAsync(string title, string message, string type = NotificationTypes.General, CancellationToken ct = default)
    {
        var ids = await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
        await DispatchAsync(ids, title, message, type, ct);
    }

    public async Task PublishToRoleAsync(string roleName, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default)
    {
        var ids = await (from ur in db.UserRoles
                         join r in db.Roles on ur.RoleId equals r.Id
                         join u in db.Users on ur.UserId equals u.Id
                         where r.Name == roleName && u.IsActive
                         select u.Id).Distinct().ToListAsync(ct);
        await DispatchAsync(ids, title, message, type, ct);
    }

    private async Task DispatchAsync(IReadOnlyList<string> userIds, string title, string message, string type, CancellationToken ct, string? link = null)
    {
        if (userIds.Count == 0) return;

        var choices = await db.Set<NotificationSubscription>().AsNoTracking()
            .Where(s => s.Type == type && userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => s.Channels, ct);

        var now = DateTime.UtcNow;
        var inApp = new List<Notification>();

        foreach (var userId in userIds)
        {
            var channels = choices.TryGetValue(userId, out var c) ? c : NotificationChannel.All;

            // Each recipient gets the text in their own language (F-Localization FR-I18N-002): their saved language, else the
            // application default. Text with no translation (for example one an administrator wrote by hand) is left as written.
            var culture = (await settings.GetAllForUserAsync(userId, ct)).GetValueOrDefault("General.DefaultLanguage") ?? "en";
            var localizedTitle = catalog.Translate(title, culture);
            var localizedMessage = catalog.Translate(message, culture);

            if (channels.HasFlag(NotificationChannel.InApp))
                inApp.Add(new Notification { UserId = userId, Type = type, Title = localizedTitle, Message = localizedMessage, Link = link, CreatedAt = now });

            if (channels.HasFlag(NotificationChannel.Email))
                db.Set<NotificationDelivery>().Add(new NotificationDelivery
                {
                    UserId = userId, Channel = NotificationChannel.Email, Type = type, Title = localizedTitle, Message = localizedMessage,
                    Status = DeliveryStatus.Pending, CreatedAt = now, NextAttemptAt = now
                });
        }

        db.Set<Notification>().AddRange(inApp);
        await db.SaveChangesAsync(ct);

        foreach (var n in inApp)
            await hub.Clients.User(n.UserId).SendAsync("notification",
                mapper.Map<NotificationDto>(n), ct);
    }
}
