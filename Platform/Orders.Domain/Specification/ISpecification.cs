using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Orders.Domain.Specification;

public sealed record OrderingExpression<T>(Expression<Func<T, object>> KeySelector, bool Descending);
public interface ISpecification<T> where T : class
{
    Expression<Func<T, bool>>? Criteria { get; }
    IReadOnlyList<string> Includes { get; }

    IReadOnlyList<OrderingExpression<T>> Ordering { get; }

    int? Skip { get; }
    int? Take { get; }
    bool AsNoTracking { get; }
}

