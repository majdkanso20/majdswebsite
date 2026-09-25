using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Exports;

/// <summary>Background exports (F-Export FR-EXP-004). Any module's <c>IExportSource</c> can be exported in the background
/// through this module without further code.</summary>
public class ExportsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<ExportWorker>();
    }
}
