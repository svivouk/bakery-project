using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Orders.Domain.Specification;

public abstract class Specification<T> : ISpecification<T> where T : class
{
    private readonly List<string> _includes = new();
    private readonly List<OrderingExpression<T>> _ordering = new();

    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public IReadOnlyList<string> Includes => _includes;
    public IReadOnlyList<OrderingExpression<T>> Ordering => _ordering;
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool AsNoTracking { get; private set; }
    protected void Where(Expression<Func<T, bool>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        Criteria = Criteria is null ? criteria : And(Criteria, criteria);
    }

    protected void Include(string navigationPath)
    {
        if (!string.IsNullOrWhiteSpace(navigationPath) && !_includes.Contains(navigationPath))
            _includes.Add(navigationPath);
    }

    protected void OrderBy(Expression<Func<T, object>> keySelector) => _ordering.Add(new(keySelector, false));

    protected void OrderByDescending(Expression<Func<T, object>> keySelector) => _ordering.Add(new(keySelector, true));
    protected void ApplySort(IReadOnlyList<SortTerm> requested, IReadOnlyDictionary<string, Expression<Func<T, object>>> allowed,
        IReadOnlyList<SortTerm> defaults)
    {
        foreach (var term in requested.Count > 0 ? requested : defaults)
        {
            if (!allowed.TryGetValue(term.Field, out var key))
                throw new ArgumentException($"Сортування за полем '{term.Field}' не підтримується.", nameof(requested));

            if (term.Descending) OrderByDescending(key);
            else OrderBy(key);
        }
    }
    protected void Paginate(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
        Skip = checked((page - 1) * pageSize);
        Take = pageSize;
    }

    protected void UseNoTracking() => AsNoTracking = true;

    private static Expression<Func<T, bool>> And(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ReplaceParameter(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, rightBody), parameter);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}


