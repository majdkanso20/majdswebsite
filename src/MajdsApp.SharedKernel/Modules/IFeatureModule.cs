using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.SharedKernel.Modules;

/// <summary>
/// Contract every feature project implements to self-register into DI and (optionally) map its own
/// endpoints (P2, FR-MOD-001). The host discovers implementations at startup — it never references a
/// feature module directly, so adding a feature means adding a project, not editing the host.
/// </summary>
public interface IFeatureModule
{
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Optional: most modules expose controllers instead, discovered as application parts.
    }
}
