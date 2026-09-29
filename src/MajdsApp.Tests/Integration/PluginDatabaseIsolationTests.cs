using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>
/// P5 FR-PLUG-011: a plugin marked <c>[PluginOwnsItsDatabase]</c> (the sample Tasks plugin) is entirely absent from
/// the host's own model and migrations — its table exists only once the plugin itself creates it, from its own
/// migration, via its <c>OnInstallAsync</c>/<c>OnUpgradeAsync</c> hook. This host, with no plugin loaded at all
/// (the default <see cref="ApiFactory"/>), proves the host side of that: nothing here ever mentions the table.
/// </summary>
public class PluginDatabaseIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_host_s_own_database_never_creates_a_plugin_owned_table()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tables = await db.Database.SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table'").ToListAsync();

        tables.Should().NotContain(t => t.Contains("Plugin_Tasks_Items", StringComparison.OrdinalIgnoreCase));
        tables.Should().NotContain(t => t.StartsWith("__MajdsApp_Plugins_Tasks", StringComparison.OrdinalIgnoreCase)); // the plugin's own migrations-history table
    }

    [Fact]
    public void The_host_s_own_model_has_no_entity_a_plugin_owns()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Model.GetEntityTypes().Should().NotContain(t => t.ClrType.Namespace != null && t.ClrType.Namespace.StartsWith("MajdsApp.Plugins.Tasks"));
    }
}
