using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Settings;

/// <summary>Replaces SharedKernel's defaults-only ISettingsProvider with the real database-backed one
/// (P2 FR-MOD-001), same pattern as F-Authorization's <c>AuthorizationModule</c>.</summary>
public class SettingsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SettingsProvider>();
        services.AddScoped<ISettingsProvider>(sp => sp.GetRequiredService<SettingsProvider>());
    }
}
