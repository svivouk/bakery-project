using Microsoft.EntityFrameworkCore;
using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories;

public static class SpecificationEvaluator
{
    public static IQueryable<T> WithSpecification<T>(this IQueryable<T> query, ISpecification<T> spec) where T : class
    {
        if (spec.AsNoTracking) query = query.AsNoTracking();

        foreach (var path in spec.Includes)
            query = query.Include(path);

        query = query.WithCriteria(spec);

        IOrderedQueryable<T>? ordered = null;
        foreach (var o in spec.Ordering)
        {
            ordered = ordered is null
                ? (o.Descending ? query.OrderByDescending(o.KeySelector) : query.OrderBy(o.KeySelector))
                : (o.Descending ? ordered.ThenByDescending(o.KeySelector) : ordered.ThenBy(o.KeySelector));
        }
        if (ordered is not null) query = ordered;

        if (spec.Skip is { } skip) query = query.Skip(skip);
        if (spec.Take is { } take) query = query.Take(take);

        return query;
    }
    public static IQueryable<T> WithCriteria<T>(this IQueryable<T> query, ISpecification<T> spec) where T : class
        => spec.Criteria is null ? query : query.Where(spec.Criteria);
}
