using System.Linq.Expressions;
using MajdsApp.SharedKernel.Paging;

namespace MajdsApp.SharedKernel.Data;

/// <summary>The repository's tie to the generic paging contract (P3 FR-REPO-007): a list endpoint gets server-side paging, sorting and filtering in one call.</summary>
public static class RepositoryPaging
{
    /// <param name="repository">Where the rows come from.</param>
    /// <param name="request">The page, size and sort the client asked for.</param>
    /// <param name="sortableColumns">The columns a client may sort by. Asking for any other is an error.</param>
    /// <param name="selector">How a row becomes the DTO (see <c>IObjectMapper.Projection</c>).</param>
    /// <param name="spec">Optional criteria (a filter, a scope) applied before paging. Its own ordering and paging are ignored: the request decides those.</param>
    public static Task<PagedResponse<TResult>> PagedAsync<TEntity, TKey, TResult>(
        this IReadRepository<TEntity, TKey> repository,
        PagedRequest request,
        IReadOnlyDictionary<string, Expression<Func<TEntity, object>>> sortableColumns,
        Expression<Func<TEntity, TResult>> selector,
        ISpecification<TEntity>? spec = null,
        CancellationToken ct = default)
        where TEntity : class, IEntity<TKey>
    {
        var query = repository.Query();
        if (spec?.Criteria is { } criteria) query = query.Where(criteria);
        query = spec?.Includes.Aggregate(query, (q, include) => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(q, include)) ?? query;
        return query.ApplyPagingAsync(request, sortableColumns, selector, ct);
    }
}
