using Orders.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<Order?> GetWithDetailsAsync(Guid orderId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, int take = 20,
            CancellationToken cancellationToken = default);
    }
}
