namespace MajdsApp.SharedKernel.Data;

/// <summary>
/// Coordinates repositories against one shared DbContext/transaction (FR-REPO-004). Command handlers
/// should not call this directly for commits — that's the job of the transaction pipeline behavior (P4);
/// inject it only when a use case genuinely needs explicit transaction control.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class, IEntity<TKey>;
    IReadRepository<TEntity, TKey> ReadRepository<TEntity, TKey>() where TEntity : class, IEntity<TKey>;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
