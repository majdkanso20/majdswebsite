using MajdsApp.Data;
using MajdsApp.SharedKernel.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Audit;

/// <summary>Failed actions in the last 24 hours (KPI). Needs the same permission as the audit log itself.</summary>
public class FailedActionsWidget(ApplicationDbContext db) : IDashboardWidget
{
    public string Key => "audit.failed-24h";
    public string Title => "Failed actions (24h)";
    public DashboardWidgetKind Kind => DashboardWidgetKind.Kpi;
    public string? Permission => Permissions.Audit.View;
    public int Order => 20;

    public async Task<object> GetDataAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var failed = await db.Set<AuditLogEntry>().CountAsync(a => !a.Succeeded && a.CreatedAt >= since, ct);
        return new KpiWidgetData(failed.ToString(), "Refused or failed in the last 24 hours");
    }
}

/// <summary>Audited actions per day for the last seven days (chart).</summary>
public class ActivityChartWidget(ApplicationDbContext db) : IDashboardWidget
{
    public string Key => "audit.activity-7d";
    public string Title => "Activity, last 7 days";
    public DashboardWidgetKind Kind => DashboardWidgetKind.Chart;
    public string? Permission => Permissions.Audit.View;
    public int Order => 30;

    public async Task<object> GetDataAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(-6);
        var timestamps = await db.Set<AuditLogEntry>().AsNoTracking()
            .Where(a => a.CreatedAt >= start).Select(a => a.CreatedAt).ToListAsync(ct);
        var perDay = timestamps.GroupBy(t => t.Date).ToDictionary(g => g.Key, g => g.Count());

        var points = Enumerable.Range(0, 7)
            .Select(offset => start.AddDays(offset))
            .Select(day => new ChartPoint(day.ToString("ddd"), perDay.GetValueOrDefault(day)))
            .ToList();
        return new ChartWidgetData(points);
    }
}

/// <summary>The latest audited actions (recent-activity feed).</summary>
public class RecentActivityWidget(ApplicationDbContext db) : IDashboardWidget
{
    public string Key => "audit.recent";
    public string Title => "Recent activity";
    public DashboardWidgetKind Kind => DashboardWidgetKind.Feed;
    public string? Permission => Permissions.Audit.View;
    public int Order => 40;
    public int Columns => 2;

    public async Task<object> GetDataAsync(CancellationToken ct)
    {
        var latest = await db.Set<AuditLogEntry>().AsNoTracking()
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Take(8)
            .Select(a => new { a.Action, a.UserName, a.Outcome, a.CreatedAt })
            .ToListAsync(ct);

        return new FeedWidgetData(latest
            .Select(a => new FeedItem(a.Action, $"{a.UserName ?? "anonymous"} · {a.Outcome}", DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc)))
            .ToList());
    }
}
