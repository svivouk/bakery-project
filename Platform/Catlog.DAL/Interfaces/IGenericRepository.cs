namespace Catalog.Dal.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<Guid> AddAsync(T entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
