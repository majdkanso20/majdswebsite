using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Plugins.Tasks;

/// <summary>The plugin's IFeatureModule entry point, named in plugin.json's "moduleType". Nothing
/// bespoke to register: its MediatR handlers/validators/controller/EF config are all discovered by
/// the same convention scans the host already runs over every module assembly (P2), because
/// PluginManager included this assembly in that scan (P5) — a plugin is not a parallel framework.</summary>
public class TasksModule : IFeatureModule, IPluginLifecycle
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    // Lifecycle hooks (P5 FR-PLUG-031): the sample has no data to seed, so it only records that they run. A real plugin would seed or clean up here.
    public Task OnInstallAsync(PluginHookContext context, CancellationToken ct) => Log(context, "installed");

    public Task OnUpgradeAsync(PluginHookContext context, string fromVersion, CancellationToken ct) => Log(context, $"upgraded from {fromVersion}");

    public Task OnEnableAsync(PluginHookContext context, CancellationToken ct) => Log(context, "enabled");

    public Task OnDisableAsync(PluginHookContext context, CancellationToken ct) => Log(context, "disabled");

    public Task OnUninstallAsync(PluginHookContext context, CancellationToken ct) => Log(context, "uninstalled");

    private static Task Log(PluginHookContext context, string what)
    {
        context.Services.GetService<ILoggerFactory>()?.CreateLogger<TasksModule>().LogInformation("Tasks plugin {What} (version {Version})", what, context.Version);
        return Task.CompletedTask;
    }
}
