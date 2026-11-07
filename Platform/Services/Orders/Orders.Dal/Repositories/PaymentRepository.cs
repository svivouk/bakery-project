using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Orders.Dal.Repositories
{
    public sealed class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(OrdersDbContext context) : base(context) { }
        public async Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => await Set
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
    }

}
