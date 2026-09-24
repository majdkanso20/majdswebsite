using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>Daily purge of notifications that were read more than 30 days ago (unread ones are kept).</summary>
public class NotificationCleanupJob(ApplicationDbContext db) : IRecurringJob
{
    public string Name => "Notification cleanup";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        await db.Set<Notification>().Where(n => n.IsRead && n.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
