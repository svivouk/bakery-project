using Microsoft.EntityFrameworkCore;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Customers;
using Orders.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories
{
    public sealed class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
    {
        public CustomerRepository(OrdersDbContext context) : base(context) { }

        public Task<Customer?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
            => Set.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);

        public async Task<IReadOnlyList<Customer>> GetCustomersWhoOrderedAsync(Guid variantId,
            CancellationToken cancellationToken = default)
        {
            var customerIds =
                from item in Context.Set<OrderItem>()
                join order in Context.Set<Order>() on item.OrderId equals order.Id
                where item.VariantId == variantId && order.Status != OrderStatus.Cancelled
                select order.CustomerId;

            return await Set
                .AsNoTracking()
                .Where(c => !c.IsDeleted && customerIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<FavouriteVariant>> GetFavouriteVariantsAsync(Guid customerId, int take = 5,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

            var lines =
                from item in Context.Set<OrderItem>()
                join order in Context.Set<Order>() on item.OrderId equals order.Id
                where order.CustomerId == customerId && order.Status != OrderStatus.Cancelled
                select new
                {
                    item.VariantId,
                    item.ProductName,
                    item.VariantName,
                    item.Quantity,
                    item.OrderId,
                    order.CreatedAt
                };

            return await lines
                .GroupBy(l => l.VariantId)
                .OrderByDescending(g => g.Sum(l => l.Quantity))
                .ThenByDescending(g => g.Max(l => l.CreatedAt))
                .Take(take)
                .Select(g => new FavouriteVariant(
                    g.Key,
                    g.Max(l => l.ProductName)!,
                    g.Max(l => l.VariantName)!,
                    g.Sum(l => l.Quantity),
                    g.Select(l => l.OrderId).Distinct().Count(),
                    g.Max(l => l.CreatedAt)))
                .ToListAsync(cancellationToken);
        }
    }
}
