using MajdsApp.Data;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// The notification dispatcher (FR-NOTIF-001): for each recipient it reads their per-type channel
/// choices, writes and pushes the in-app notification in real time, and queues email for the
/// background worker. Callers depend only on <see cref="IUserNotificationPublisher"/>.
/// </summary>
public class NotificationPublisher(ApplicationDbContext db, IHubContext<NotificationHub> hub) : IUserNotificationPublisher
{
    public Task PublishAsync(string userId, string title, string message, string type = NotificationTypes.General, CancellationToken ct = default) =>
        DispatchAsync([userId], title, message, type, ct);

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

    private async Task DispatchAsync(IReadOnlyList<string> userIds, string title, string message, string type, CancellationToken ct)
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

            if (channels.HasFlag(NotificationChannel.InApp))
                inApp.Add(new Notification { UserId = userId, Type = type, Title = title, Message = message, CreatedAt = now });

            if (channels.HasFlag(NotificationChannel.Email))
                db.Set<NotificationDelivery>().Add(new NotificationDelivery
                {
                    UserId = userId, Channel = NotificationChannel.Email, Type = type, Title = title, Message = message,
                    Status = DeliveryStatus.Pending, CreatedAt = now, NextAttemptAt = now
                });
        }

        db.Set<Notification>().AddRange(inApp);
        await db.SaveChangesAsync(ct);

        foreach (var n in inApp)
            await hub.Clients.User(n.UserId).SendAsync("notification",
                new NotificationDto(n.Id, n.Type, n.Title, n.Message, false, n.CreatedAt), ct);
    }
}
