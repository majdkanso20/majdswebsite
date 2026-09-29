using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Plugins.Tasks;

/// <summary>
/// The Tasks plugin's own database (P5 FR-PLUG-011): its entity and its migrations are entirely its own,
/// independent of the host's <c>ApplicationDbContext</c> and of any other plugin's (see the assembly-level
/// <c>PluginOwnsItsDatabase</c> attribute in <c>TasksModule.cs</c>) — same physical database (connection string),
/// a separate migrations-history table (<see cref="MajdsApp.SharedKernel.Plugins.PluginMigrations"/> names it),
/// so the two are never confused. Registered with the same central soft-delete/audit-stamping interceptor the
/// host wires into its own context (<see cref="TasksModule.ConfigureServices"/>), since that behavior has
/// nothing to do with which context an entity lives in.
/// </summary>
public class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new TaskItemConfiguration());
    }
}
