using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Dashboard;

/// <summary>The dashboard shell (F-Dashboard). It owns no widgets: every module registers its own
/// <c>IDashboardWidget</c>, and this module lists the ones the caller may see and serves their data.</summary>
public class DashboardModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Handlers and the controller are discovered by convention.
    }
}
