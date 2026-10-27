using Microsoft.EntityFrameworkCore;
using Orders.Dal.Repositories.Interfaces;

namespace Orders.Dal.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly OrdersDbContext Context;
        protected readonly DbSet<T> Set;
        public GenericRepository(OrdersDbContext context)
        {
            Context = context;
            Set = context.Set<T>();
        }
        public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await Set.FindAsync(new object[] { id }, ct);
        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
            => await Set.AsNoTracking().ToListAsync(ct);
        public async Task AddAsync(T entity, CancellationToken ct = default)
            => await Set.AddAsync(entity, ct);
        public void Update(T entity) => Set.Update(entity);
        public void Delete(T entity) => Set.Remove(entity);
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => Context.SaveChangesAsync(ct);
    }
}
