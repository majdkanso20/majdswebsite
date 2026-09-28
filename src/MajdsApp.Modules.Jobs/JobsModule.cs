using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Jobs;

/// <summary>The <c>Jobs</c> section (P2 FR-MOD-004). Each part can be switched off, for example to run the scheduler on one node only or so a test can drive the work itself.</summary>
public class JobsOptions
{
    public JobPartOptions Scheduler { get; set; } = new();
    public JobPartOptions Worker { get; set; } = new();
}

public class JobPartOptions
{
    public bool Enabled { get; set; } = true;
}

public class JobsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Bound and checked at start: a value that is not true or false (Jobs:Worker:Enabled=nope) stops the start instead of quietly meaning "on".
        services.AddModuleOptions<JobsOptions>(configuration, "Jobs");
        var jobs = configuration.GetSection("Jobs").Get<JobsOptions>() ?? new JobsOptions();

        if (jobs.Scheduler.Enabled) services.AddHostedService<JobScheduler>();
        if (jobs.Worker.Enabled) services.AddHostedService<BackgroundJobWorker>();
        services.AddScoped<MajdsApp.SharedKernel.Jobs.IBackgroundJobQueue, BackgroundJobQueue>();
    }
}
