using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Audit;

/// <summary>Replaces SharedKernel's no-op IAuditLogWriter with the real database-backed one (P2
/// FR-MOD-001), same pattern as F-Authorization's <c>AuthorizationModule</c>.</summary>
public class AuditModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditLogWriter>();
        services.AddScoped<IAuditLogWriter>(sp => sp.GetRequiredService<AuditLogWriter>());

        services.AddScoped<AuditExportSource>();
        services.AddScoped<MajdsApp.SharedKernel.Export.IExportSource>(sp => sp.GetRequiredService<AuditExportSource>());

        services.AddScoped<MajdsApp.SharedKernel.Dashboard.IDashboardWidget, FailedActionsWidget>();
        services.AddScoped<MajdsApp.SharedKernel.Dashboard.IDashboardWidget, ActivityChartWidget>();
        services.AddScoped<MajdsApp.SharedKernel.Dashboard.IDashboardWidget, RecentActivityWidget>();
    }
}
