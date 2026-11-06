using Microsoft.EntityFrameworkCore;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Promotions;
using Orders.Domain.Specification;

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
        public async Task<IReadOnlyList<PromoCode>> ListAsync(ISpecification<PromoCode> specification, CancellationToken ct = default)
            => await Set.WithSpecification(specification).ToListAsync(ct);

        public Task<int> CountAsync(ISpecification<PromoCode> specification, CancellationToken ct = default)
            => Set.AsNoTracking().WithCriteria(specification).CountAsync(ct);
    }
}
