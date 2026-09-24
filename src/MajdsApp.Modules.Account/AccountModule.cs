using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Account;

public class AccountModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Handlers and the controller are discovered by convention.
    }
}
