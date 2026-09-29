using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Users;

public class UsersModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // SuperAdminGuard self-registers via IScopedService; handlers/validators/controller by convention.
        services.AddScoped<MajdsApp.SharedKernel.Dashboard.IDashboardWidget, TotalUsersWidget>();
        services.AddScoped<UsersExportSource>();
        services.AddScoped<MajdsApp.SharedKernel.Export.IExportSource>(sp => sp.GetRequiredService<UsersExportSource>());
        services.AddScoped<UsersImportSource>();
        services.AddScoped<MajdsApp.SharedKernel.Import.IImportSource>(sp => sp.GetRequiredService<UsersImportSource>());
    }
}
