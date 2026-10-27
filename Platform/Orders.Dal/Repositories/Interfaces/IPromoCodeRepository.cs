using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public interface IPromoCodeRepository : IGenericRepository<PromoCode>
    {        
        Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<PromoCode?> GetWithUsageAsync(Guid id, CancellationToken cancellationToken = default);

        Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}
