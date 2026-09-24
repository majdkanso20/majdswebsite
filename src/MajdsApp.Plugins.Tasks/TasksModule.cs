using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Plugins.Tasks;

/// <summary>The plugin's IFeatureModule entry point, named in plugin.json's "moduleType". Nothing
/// bespoke to register: its MediatR handlers/validators/controller/EF config are all discovered by
/// the same convention scans the host already runs over every module assembly (P2), because
/// PluginManager included this assembly in that scan (P5) — a plugin is not a parallel framework.</summary>
public class TasksModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}
