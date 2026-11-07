using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain.Specification;

public static class SpecificationQueryExtensions
{
    public static IQueryable<T> ApplyCriteria<T>(this IQueryable<T> query, ISpecification<T> spec) where T : class
        => spec.Criteria is null ? query : query.Where(spec.Criteria);
    public static IQueryable<T> ApplySpecification<T>(this IQueryable<T> query, ISpecification<T> spec) where T : class
    {
        query = query.ApplyCriteria(spec);

        IOrderedQueryable<T>? ordered = null;
        foreach (var order in spec.Ordering)
        {
            ordered = ordered is null
                ? (order.Descending ? query.OrderByDescending(order.KeySelector) : query.OrderBy(order.KeySelector))
                : (order.Descending ? ordered.ThenByDescending(order.KeySelector) : ordered.ThenBy(order.KeySelector));
        }
        if (ordered is not null) query = ordered;

        if (spec.Skip is { } skip) query = query.Skip(skip);
        if (spec.Take is { } take) query = query.Take(take);
        return query;
    }
}
