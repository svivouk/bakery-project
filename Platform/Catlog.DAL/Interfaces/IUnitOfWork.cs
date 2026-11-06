using System.Data;

namespace Catalog.Dal.Interfaces;

///  
/// Одне з'єднання і (за потреби) одна транзакція на HTTP-запит (Scoped).
/// Усі репозиторії, отримані з одного UoW, працюють у спільній транзакції.
///  
public interface IUnitOfWork : IAsyncDisposable
{
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    IVariantRepository Variants { get; }
    IStockRepository Stock { get; }
    IOutboxRepository Outbox { get; }
    IReferenceRepository Reference { get; }

    bool HasActiveTransaction { get; }

    Task BeginAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);

    ///  Begin → work → Commit; при будь-якому винятку — Rollback і повторне викидання. 
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
    Task InTransactionAsync(Func<Task> work, CancellationToken ct = default);
}
