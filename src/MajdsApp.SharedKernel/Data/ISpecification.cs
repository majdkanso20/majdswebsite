using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.SharedKernel.Data;

/// <summary>
/// Specification pattern (FR-REPO-003): reusable, testable query logic — criteria, includes,
/// ordering, and paging — that repositories materialize instead of callers composing IQueryable ad hoc.
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Skip { get; }
    int? Take { get; }
}

public abstract class Specification<T> : ISpecification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public List<Expression<Func<T, object>>> Includes { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }

    protected Specification() { }

    protected Specification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    protected void ApplyCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;
    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);
    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) => OrderByDescending = orderByDescending;

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }
}

/// <summary>Applies an <see cref="ISpecification{T}"/> to an <see cref="IQueryable{T}"/> (used by repositories).</summary>
public static class SpecificationEvaluator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, ISpecification<T> spec) where T : class
    {
        if (spec.Criteria is not null)
            query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            query = query.OrderByDescending(spec.OrderByDescending);

        if (spec.Skip.HasValue)
            query = query.Skip(spec.Skip.Value);
        if (spec.Take.HasValue)
            query = query.Take(spec.Take.Value);

        return query;
    }
}
