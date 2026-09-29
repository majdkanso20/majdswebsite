namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// Marks a plugin's assembly (P5 FR-PLUG-011): its EF Core entities are not part of the host's shared
/// <c>ApplicationDbContext</c> model — the plugin has its own <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// and ships its own migrations, applied by its <see cref="IPluginLifecycle.OnInstallAsync"/>/<see cref="IPluginLifecycle.OnUpgradeAsync"/>
/// hook through <see cref="PluginMigrations"/>, so its schema (and the data in it) is entirely independent of the
/// host's and of any other plugin's, and cleanly removable. A plugin with one small table can skip all of this
/// and just add a normal <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/> to the
/// shared context with a prefixed table name instead — this attribute is for a plugin that wants its data kept
/// apart, not a requirement every plugin has to meet.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PluginOwnsItsDatabaseAttribute : Attribute;
