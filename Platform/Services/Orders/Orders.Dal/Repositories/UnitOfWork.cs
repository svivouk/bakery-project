using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Catalog;
using Orders.Domain.Messaging;
using Orders.Domain.Payments;
using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly OrdersDbContext _context;

        private IOrderRepository? _orders;
        private ICartRepository? _carts;
        private ICustomerRepository? _customers;
        private IPromoCodeRepository? _promoCodes;
        private IPaymentRepository? _payments;
        private IGenericRepository<ProductRef>? _productRefs;
        private IGenericRepository<VariantRef>? _variantRefs;
        private IGenericRepository<OutboxMessage>? _outbox;
        private IGenericRepository<InboxMessage>? _inbox;

        public UnitOfWork(OrdersDbContext context)
        {
            _context = context;
        }

        public IOrderRepository Orders => _orders ??= new OrderRepository(_context);
        public ICartRepository Carts => _carts ??= new CartRepository(_context);
        public ICustomerRepository Customers => _customers ??= new CustomerRepository(_context);
        public IPromoCodeRepository PromoCodes => _promoCodes ??= new PromoCodeRepository(_context);
        public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);
        public IGenericRepository<ProductRef> ProductRefs => _productRefs ??= new GenericRepository<ProductRef>(_context);
        public IGenericRepository<VariantRef> VariantRefs => _variantRefs ??= new GenericRepository<VariantRef>(_context);
        public IGenericRepository<OutboxMessage> Outbox => _outbox ??= new GenericRepository<OutboxMessage>(_context);
        public IGenericRepository<InboxMessage> Inbox => _inbox ??= new GenericRepository<InboxMessage>(_context);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }

}
