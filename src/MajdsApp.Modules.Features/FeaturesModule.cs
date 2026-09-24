using MajdsApp.SharedKernel.Features;
using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Features;

public static class Permissions
{
    public static class Features
    {
        public const string View = "Features.View";
        public const string Edit = "Features.Edit";
    }
}

public class FeaturesModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IFeatureChecker, FeatureChecker>();
    }
}
