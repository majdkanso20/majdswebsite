using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Imports;

/// <summary>Background imports (F-Export FR-EXP-003/004). Any module's <c>IImportSource</c> can be imported in the
/// background through this module without further code — the counterpart to F-Export's own module for exports.</summary>
public class ImportsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<ImportWorker>();
    }
}
