using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.SharedKernel.Data;

/// <summary>Generic EF Core repository (P3) shared by every feature module — no bespoke repositories.</summary>
public class EfRepository<TEntity, TKey>(DbContext context) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    protected DbSet<TEntity> Set { get; } = context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(BuildIdPredicate(id), ct);

    public IQueryable<TEntity> Query() => Set.AsQueryable();

    public IQueryable<TEntity> QueryNoTracking() => Set.AsNoTracking();

    public async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> spec, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set.AsQueryable(), spec).ToListAsync(ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default) =>
        await Set.AddRangeAsync(entities, ct);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity)
    {
        if (entity is ISoftDelete softDelete)
        {
            softDelete.IsDeleted = true;
            softDelete.DeletedAt = DateTime.UtcNow;
            Set.Update(entity);
        }
        else
        {
            Set.Remove(entity);
        }
    }

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        Set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(ISpecification<TEntity>? spec = null, CancellationToken ct = default) =>
        spec is null
            ? Set.CountAsync(ct)
            : SpecificationEvaluator.Apply(Set.AsQueryable(), spec).CountAsync(ct);

    internal static Expression<Func<TEntity, bool>> IdIs(TKey id) => BuildIdPredicate(id);

    private static Expression<Func<TEntity, bool>> BuildIdPredicate(TKey id)
    {
        // entity => EF.Property<TKey>(entity, "Id").Equals(id) — avoids requiring a concrete Id property shape.
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var idProperty = Expression.Property(parameter, nameof(IEntity<TKey>.Id));
        var idValue = Expression.Constant(id, typeof(TKey));
        var equals = Expression.Equal(idProperty, idValue);
        return Expression.Lambda<Func<TEntity, bool>>(equals, parameter);
    }
}
