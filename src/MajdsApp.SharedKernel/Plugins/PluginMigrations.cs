using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// What a plugin marked <see cref="PluginOwnsItsDatabaseAttribute"/> calls, from its <see cref="IPluginLifecycle.OnInstallAsync"/>
/// or <see cref="IPluginLifecycle.OnUpgradeAsync"/> hook, to bring its own database up to date (P5 FR-PLUG-011/032):
/// applies the migrations compiled into <typeparamref name="TContext"/>'s own assembly against the host's database
/// (the same connection <c>ApplicationDbContext</c> uses, so it is one physical database, but a separate
/// <c>__&lt;PluginId&gt;MigrationsHistory</c> table keeps the two entirely independent — a plugin's migrations
/// are never confused with the host's or another plugin's). SQLite wraps a migration's DDL in a transaction where
/// it can (creating a new table, the common case for a plugin), so a failure partway through does not leave the
/// database half-changed; a failure still propagates to the caller, which is already how <see cref="PluginHooks.RunAsync"/>
/// handles a failing lifecycle hook — the plugin is reported failed and the host stays healthy (FR-PLUG-032).
/// </summary>
public static class PluginMigrations
{
    public static async Task ApplyAsync<TContext>(PluginHookContext context, CancellationToken ct = default) where TContext : DbContext
    {
        var configuration = context.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connectionString, sql => sql.MigrationsHistoryTable(HistoryTableName(context.PluginId)))
            .Options;

        using var db = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>A plugin id can contain characters SQLite would need to quote (dots, for example); kept simple and
    /// readable rather than passing them through and relying on EF to quote correctly everywhere.</summary>
    private static string HistoryTableName(string pluginId) =>
        "__" + new string(pluginId.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()) + "MigrationsHistory";
}
