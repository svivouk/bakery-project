using Orders.Domain.Carts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public interface ICartRepository : IGenericRepository<Cart>
    {
        Task<Cart?> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
        Task LoadItemsAsync(Cart cart, CancellationToken cancellationToken = default); 
        Task<int> CountItemsAsync(Cart cart, CancellationToken cancellationToken = default);
        Task<Cart?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default);

    }
}
