using Microsoft.EntityFrameworkCore;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories
{
    public sealed class OrderRepository : GenericRepository<Order>, IOrderRepository
    {
        public OrderRepository(OrdersDbContext context) : base(context) { }
        public Task<Order?> GetWithDetailsAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Set
                .Include(o => o.Items)
                .Include(o => o.Delivery)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, int take = 20,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

            return await Set
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Take(take)
                .Include(o => o.Items)
                .Include(o => o.Delivery)
                .ToListAsync(cancellationToken);
        }
    }

}
