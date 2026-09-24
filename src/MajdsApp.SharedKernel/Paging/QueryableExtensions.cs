using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.SharedKernel.Paging;

public static class QueryableExtensions
{
    /// <summary>
    /// Applies sort (from an explicit column allow-list, preventing injection via the `sort` string
    /// per NFR-SEC-3) and paging at the database, then materializes into a <see cref="PagedResponse{T}"/>
    /// (FR-GRID-003/004). One line per list endpoint (Agent Notes, F-Data).
    /// </summary>
    public static Task<PagedResponse<T>> ApplyPagingAsync<T>(
        this IQueryable<T> query,
        PagedRequest request,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> sortableColumns,
        CancellationToken ct = default) =>
        query.ApplyPagingAsync(request, sortableColumns, selector: x => x, ct);

    /// <summary>
    /// Same as above, but sorts/counts/pages on <typeparamref name="TSource"/> (the entity) first and
    /// only projects to <typeparamref name="TResult"/> (a DTO) for the final page. Sorting/paging
    /// after a DTO projection can fail to translate in EF Core for non-trivial projections, so always
    /// order the entity query, then project last.
    /// </summary>
    public static async Task<PagedResponse<TResult>> ApplyPagingAsync<TSource, TResult>(
        this IQueryable<TSource> query,
        PagedRequest request,
        IReadOnlyDictionary<string, Expression<Func<TSource, object>>> sortableColumns,
        Expression<Func<TSource, TResult>> selector,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Sort))
        {
            var parts = request.Sort.Split(':', 2);
            var column = parts[0];
            var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            if (sortableColumns.TryGetValue(column, out var sortSelector))
            {
                query = descending ? query.OrderByDescending(sortSelector) : query.OrderBy(sortSelector);
            }
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(selector)
            .ToListAsync(ct);

        return new PagedResponse<TResult>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
