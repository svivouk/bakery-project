using Orders.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Orders.Domain.Specification;

public sealed class OrderListSpecification : Specification<Order>
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<Order, object>>> SortMap =
        new Dictionary<string, Expression<Func<Order, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["orderNumber"] = o => o.OrderNumber,
            ["createdAt"] = o => o.CreatedAt,
            ["desiredReadyAt"] = o => o.DesiredReadyAt,
            ["totalAmount"] = o => o.TotalAmount
        };

    public static IReadOnlyCollection<string> SortableFields => SortMap.Keys.ToArray();
    public static readonly IReadOnlyList<SortTerm> DefaultSort = [new("createdAt", true)];
    public OrderListSpecification(Guid? customerId, OrderStatus? status, DateTime? createdFrom, DateTime? createdTo,
        decimal? minTotal, decimal? maxTotal, long? orderNumber,
        IReadOnlyList<SortTerm> sort, int page, int pageSize)
    {
        if (customerId is { } customer)
            Where(o => o.CustomerId == customer);

        if (status is { } s)
            Where(o => o.Status == s);

        if (createdFrom is { } from)
            Where(o => o.CreatedAt >= from);

        if (createdTo is { } to)
            Where(o => o.CreatedAt <= to);

        if (minTotal is { } min)
            Where(o => o.TotalAmount >= min);

        if (maxTotal is { } max)
            Where(o => o.TotalAmount <= max);

        if (orderNumber is { } number)
            Where(o => o.OrderNumber == number);

        UseNoTracking();

        ApplySort(sort, SortMap, DefaultSort);
        OrderBy(o => o.Id);   

        Paginate(page, pageSize);
    }
}