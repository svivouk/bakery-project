using Orders.Domain.Catalog;
using Orders.Domain.Messaging;
using Orders.Domain.Payments;
using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public interface IUnitOfWork
    {
        IOrderRepository Orders { get; }
        ICartRepository Carts { get; }
        ICustomerRepository Customers { get; }
        IPromoCodeRepository PromoCodes { get; }
        IPaymentRepository Payments { get; }

        IGenericRepository<ProductRef> ProductRefs { get; }
        IGenericRepository<VariantRef> VariantRefs { get; }

        IGenericRepository<OutboxMessage> Outbox { get; }
        IGenericRepository<InboxMessage> Inbox { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }

}
