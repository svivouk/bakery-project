using Orders.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    }
}
