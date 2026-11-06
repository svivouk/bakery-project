using Microsoft.EntityFrameworkCore;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Carts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories
{
    public sealed class CartRepository : GenericRepository<Cart>, ICartRepository
    {
        public CartRepository(OrdersDbContext context) : base(context) { }
        public Task<Cart?> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
       => Set.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        public async Task LoadItemsAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(cart);

            var entry = Context.Entry(cart);
            if (entry.State is EntityState.Detached or EntityState.Added) return;

            var items = entry.Collection(c => c.Items);
            if (!items.IsLoaded)
                await items.LoadAsync(cancellationToken);
        }

        public async Task<Cart?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var cart = await GetByCustomerAsync(customerId, cancellationToken);
            if (cart is not null)
                await LoadItemsAsync(cart, cancellationToken);
            return cart;
        }

        public async Task<int> CountItemsAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(cart);

            var items = Context.Entry(cart).Collection(c => c.Items);
            return items.IsLoaded
                ? cart.Items.Count
                : await items.Query().CountAsync(cancellationToken); 
        }
    }

}

