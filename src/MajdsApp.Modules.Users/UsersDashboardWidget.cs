using MajdsApp.Data;
using MajdsApp.SharedKernel.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

/// <summary>Registered and active user counts (KPI).</summary>
public class TotalUsersWidget(ApplicationDbContext db, MajdsApp.SharedKernel.Localization.IMessageCatalog catalog) : IDashboardWidget
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
        // The caption is a sentence with a number in it, so it is translated here, into the language of this request, rather than by the screen.
        return new KpiWidgetData(total.ToString(), catalog.Translate($"{active} active", System.Globalization.CultureInfo.CurrentUICulture.Name));
    }
}
