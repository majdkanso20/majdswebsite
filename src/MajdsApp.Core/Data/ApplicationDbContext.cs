using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Data;

/// <summary>
/// The one shared DbContext every feature module contributes entity configurations to (P3 FR-MOD-006).
/// Modules never get their own DbContext or fork this one — they add an <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>
/// and it's picked up here automatically, so this class does not change per module (Open/Closed).
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().HasQueryFilter(u => !u.IsDeleted);

        // Includes MajdsApp.Plugins.* too (P5): a runtime-loaded plugin's IEntityTypeConfiguration is
        // picked up the same way a compiled-in module's is, as long as its assembly is already loaded
        // by the time this model is built (PluginManager loads plugins before AddDbContext runs).
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } name
                && (name.StartsWith("MajdsApp.Modules.", StringComparison.Ordinal) || name.StartsWith("MajdsApp.Plugins.", StringComparison.Ordinal)));

        foreach (var assembly in moduleAssemblies)
            builder.ApplyConfigurationsFromAssembly(assembly);
    }
}
