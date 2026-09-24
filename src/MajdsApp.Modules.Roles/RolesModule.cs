using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Roles;

public class RolesModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing bespoke: handlers/validators/controller are discovered by convention.
    }
}
