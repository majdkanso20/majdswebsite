using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Jobs;

public class JobsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Both can be switched off by configuration (Jobs:Scheduler:Enabled, Jobs:Worker:Enabled; on unless set to "false"), for example
        // to run them on one node only, or so tests can drive the work themselves and never touch real data.
        if (configuration["Jobs:Scheduler:Enabled"] is not "false") services.AddHostedService<JobScheduler>();
        if (configuration["Jobs:Worker:Enabled"] is not "false") services.AddHostedService<BackgroundJobWorker>();
        services.AddScoped<MajdsApp.SharedKernel.Jobs.IBackgroundJobQueue, BackgroundJobQueue>();
    }
}
