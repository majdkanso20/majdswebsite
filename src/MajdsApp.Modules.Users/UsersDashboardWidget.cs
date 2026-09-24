using MajdsApp.Data;
using MajdsApp.SharedKernel.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

/// <summary>Registered and active user counts (KPI).</summary>
public class TotalUsersWidget(ApplicationDbContext db) : IDashboardWidget
{
    public string Key => "users.total";
    public string Title => "Users";
    public DashboardWidgetKind Kind => DashboardWidgetKind.Kpi;
    public string? Permission => MajdsApp.Modules.Authorization.Permissions.Users.View;
    public int Order => 10;

    public async Task<object> GetDataAsync(CancellationToken ct)
    {
        var total = await db.Users.CountAsync(ct);
        var active = await db.Users.CountAsync(u => u.IsActive, ct);
        return new KpiWidgetData(total.ToString(), $"{active} active");
    }
}
