using Orders.Domain.Customers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public sealed record FavouriteVariant(
    Guid VariantId,
    string ProductName,
    string VariantName,
    int TotalQuantity,
    int OrdersCount,
    DateTime LastOrderedAt);
    public interface ICustomerRepository : IGenericRepository<Customer>
    {
        Task<Customer?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Customer>> GetCustomersWhoOrderedAsync(Guid variantId,
        CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FavouriteVariant>> GetFavouriteVariantsAsync(Guid customerId, int take = 5,
        CancellationToken cancellationToken = default);
    }
}
