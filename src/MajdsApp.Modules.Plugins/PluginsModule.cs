using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Plugins;

public class PluginsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Registered after the SharedKernel's TryAddSingleton default (AllowAllPluginStateCache) runs,
        // so this wins on resolution — same override idiom as every other cross-cutting default.
        services.AddSingleton<PluginStateCache>();
        services.AddSingleton<IPluginStateCache>(sp => sp.GetRequiredService<PluginStateCache>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PluginAssetEndpoint.Map(endpoints);
}
