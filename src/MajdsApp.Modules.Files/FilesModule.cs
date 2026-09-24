using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Files;

public class FilesModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // FileStorage is picked up by convention (IScopedService); nothing bespoke to register.
    }
}
