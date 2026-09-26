using Microsoft.EntityFrameworkCore;

namespace MajdsApp.SharedKernel.Data;

/// <summary>The read-only repository (P3 FR-REPO-002): every query it returns is no-tracking, so a query path can never modify what it reads.</summary>
public class EfReadRepository<TEntity, TKey>(DbContext context) : IReadRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    private DbSet<TEntity> Set { get; } = context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(EfRepository<TEntity, TKey>.IdIs(id), ct);

    public IQueryable<TEntity> Query() => Set.AsNoTracking();

    public async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> spec, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set.AsNoTracking(), spec).ToListAsync(ct);

    public Task<int> CountAsync(ISpecification<TEntity>? spec = null, CancellationToken ct = default) =>
        spec is null ? Set.CountAsync(ct) : SpecificationEvaluator.Apply(Set.AsNoTracking(), spec).CountAsync(ct);
}
