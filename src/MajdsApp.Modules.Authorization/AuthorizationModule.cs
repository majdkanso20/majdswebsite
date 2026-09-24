using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Authorization;

/// <summary>Replaces SharedKernel's permissive default IPermissionChecker with the real one (P2 FR-MOD-001).</summary>
public class AuthorizationModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PermissionChecker>();
        services.AddScoped<IPermissionChecker>(sp => sp.GetRequiredService<PermissionChecker>());
    }
}
