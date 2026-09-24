using System.Linq.Expressions;

namespace MajdsApp.SharedKernel.Data;

/// <summary>Generic repository (FR-REPO-001). Read paths should prefer <see cref="IReadRepository{TEntity,TKey}"/>.</summary>
public interface IRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);
    IQueryable<TEntity> Query();
    IQueryable<TEntity> QueryNoTracking();
    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> spec, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<TEntity>? spec = null, CancellationToken ct = default);
}

/// <summary>Read-only repository (FR-REPO-002): never tracks entities. Use on all query paths.</summary>
public interface IReadRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);
    IQueryable<TEntity> Query();
    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<TEntity>? spec = null, CancellationToken ct = default);
}
