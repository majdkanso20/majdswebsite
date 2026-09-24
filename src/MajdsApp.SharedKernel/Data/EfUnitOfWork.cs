using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.SharedKernel.Data;

/// <summary>
/// Unit of work over a shared <see cref="DbContext"/> (FR-REPO-004). Register the concrete DbContext
/// itself as scoped, then register <c>IUnitOfWork</c> as this class closed over that DbContext type
/// so every repository resolved through it shares one context/transaction per request.
/// </summary>
public class EfUnitOfWork<TDbContext>(TDbContext context) : IUnitOfWork where TDbContext : DbContext
{
    private IDbContextTransaction? _transaction;

    public IRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class, IEntity<TKey> =>
        new EfRepository<TEntity, TKey>(context);

    public IReadRepository<TEntity, TKey> ReadRepository<TEntity, TKey>() where TEntity : class, IEntity<TKey> =>
        new EfRepository<TEntity, TKey>(context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default) =>
        _transaction = await context.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        try
        {
            await context.SaveChangesAsync(ct);
            if (_transaction is not null)
                await _transaction.CommitAsync(ct);
        }
        finally
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
    }
}
