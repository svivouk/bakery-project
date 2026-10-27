using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Promotions;
using Microsoft.EntityFrameworkCore;

namespace Orders.Dal.Repositories
{
    public sealed class PromoCodeRepository : GenericRepository<PromoCode>, IPromoCodeRepository
    {
        public PromoCodeRepository(OrdersDbContext context) : base(context) { }
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Set.AsNoTracking().AnyAsync(p => p.Code == code && !p.IsDeleted, cancellationToken);

        public Task<PromoCode?> GetWithUsageAsync(Guid id, CancellationToken cancellationToken = default)
            => Set.Include(p => p.Usage).FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        public Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Set.Include(p => p.Usage).FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted, cancellationToken);
    }
}
