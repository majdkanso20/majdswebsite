using MajdsApp.Data;
using MajdsApp.SharedKernel.Dashboard;
using MajdsApp.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>The caller's own unread notifications (KPI). Scoped to the signed-in user, so it needs no permission.</summary>
public class UnreadNotificationsWidget(ApplicationDbContext db, ICurrentUser currentUser) : IDashboardWidget
{
    public string Key => "notifications.unread";
    public string Title => "Unread notifications";
    public DashboardWidgetKind Kind => DashboardWidgetKind.Kpi;
    public string? Permission => null;
    public int Order => 5;

    public async Task<object> GetDataAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var unread = await db.Set<Notification>().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return new KpiWidgetData(unread.ToString(), unread == 0 ? "You are all caught up" : "Waiting for you");
    }
}
