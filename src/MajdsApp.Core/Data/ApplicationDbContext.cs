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

        // Any entity that supports soft delete is hidden once deleted, without each module having to remember the filter (P3 FR-REPO-005).
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => t.BaseType is null && typeof(MajdsApp.SharedKernel.Data.ISoftDelete).IsAssignableFrom(t.ClrType) && !t.GetDeclaredQueryFilters().Any()).ToList())
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var notDeleted = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Property(parameter, nameof(MajdsApp.SharedKernel.Data.ISoftDelete.IsDeleted)));
            builder.Entity(entityType.ClrType).HasQueryFilter(System.Linq.Expressions.Expression.Lambda(notDeleted, parameter));
        }
    }
}
