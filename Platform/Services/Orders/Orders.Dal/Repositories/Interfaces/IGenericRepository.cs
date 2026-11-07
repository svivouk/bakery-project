using Orders.Domain;
using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Dal.Repositories.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {

        Task AddAsync(T entity, CancellationToken ct = default);
        Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
        void Update(T entity);
        void Delete(T entity);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default);
        Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken ct = default);
        Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default);
    }
}
