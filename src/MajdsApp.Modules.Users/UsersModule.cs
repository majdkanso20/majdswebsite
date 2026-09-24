using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Users;

public class UsersModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // SuperAdminGuard self-registers via IScopedService; handlers/validators/controller by convention.
    }
}
