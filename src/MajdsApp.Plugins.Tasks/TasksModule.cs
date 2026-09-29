using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// This plugin's data lives in its own TasksDbContext with its own migrations, not the host's shared
// ApplicationDbContext (P5 FR-PLUG-011): the table-name-prefix option most plugins use is simpler, but
// this sample demonstrates the fuller isolation the SRS also names.
[assembly: PluginOwnsItsDatabase]

namespace MajdsApp.Plugins.Tasks;

/// <summary>The plugin's IFeatureModule entry point, named in plugin.json's "moduleType". Its MediatR
/// handlers/validators/controller are discovered by the same convention scans the host already runs
/// over every module assembly (P2), because PluginManager included this assembly in that scan (P5) —
/// a plugin is not a parallel framework. TasksDbContext is the one thing registered here by hand,
/// since [PluginOwnsItsDatabase] keeps it out of the host's own assembly-configuration scan.</summary>
public class TasksModule : IFeatureModule, IPluginLifecycle
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Same interceptor the host's own ApplicationDbContext uses (registered by the host, before any
        // module's ConfigureServices runs): CreatedAt/By stamping and soft delete are not host-specific.
        services.AddDbContext<TasksDbContext>((sp, options) =>
            options.UseSqlite(connectionString).AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
    }

    // Lifecycle hooks (P5 FR-PLUG-031). OnInstall/OnUpgrade bring this plugin's own database up to date
    // (FR-PLUG-011/032) before anything else about the plugin runs.
    public Task OnInstallAsync(PluginHookContext context, CancellationToken ct) =>
        MigrateAndLog(context, "installed");

    public Task OnUpgradeAsync(PluginHookContext context, string fromVersion, CancellationToken ct) =>
        MigrateAndLog(context, $"upgraded from {fromVersion}");

    public Task OnEnableAsync(PluginHookContext context, CancellationToken ct) => Log(context, "enabled");

    public Task OnDisableAsync(PluginHookContext context, CancellationToken ct) => Log(context, "disabled");

    public Task OnUninstallAsync(PluginHookContext context, CancellationToken ct) => Log(context, "uninstalled");

    private static async Task MigrateAndLog(PluginHookContext context, string what)
    {
        await PluginMigrations.ApplyAsync<TasksDbContext>(context);
        await Log(context, what);
    }

    private static Task Log(PluginHookContext context, string what)
    {
        context.Services.GetService<ILoggerFactory>()?.CreateLogger<TasksModule>().LogInformation("Tasks plugin {What} (version {Version})", what, context.Version);
        return Task.CompletedTask;
    }
}
