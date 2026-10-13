using System.Data;
using Catalog.Dal.Interfaces;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

/// <summary>
/// Володіє єдиним SqlConnection і поточною SqlTransaction. Репозиторії створюються ліниво й отримують
/// з'єднання та делегат на поточну транзакцію. Реєструється як Scoped: один екземпляр на HTTP-запит.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    private ICategoryRepository? _categories;
    private IProductRepository? _products;
    private IVariantRepository? _variants;
    private IStockRepository? _stock;
    private IOutboxRepository? _outbox;
    private IReferenceRepository? _reference;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
    }

    public ICategoryRepository Categories => _categories ??= new CategoryRepository(_connection, CurrentTx);
    public IProductRepository Products => _products ??= new ProductRepository(_connection, CurrentTx);
    public IVariantRepository Variants => _variants ??= new VariantRepository(_connection, CurrentTx);
    public IStockRepository Stock => _stock ??= new StockRepository(_connection, CurrentTx);
    public IOutboxRepository Outbox => _outbox ??= new OutboxRepository(_connection, CurrentTx);
    public IReferenceRepository Reference => _reference ??= new ReferenceRepository(_connection, CurrentTx);

    public bool HasActiveTransaction => _transaction is not null;

    private SqlTransaction? CurrentTx() => _transaction;

    public async Task BeginAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Транзакцію вже розпочато.");

        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(isolationLevel, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        var tx = _transaction ?? throw new InvalidOperationException("Немає активної транзакції.");
        try
        {
            await tx.CommitAsync(ct);
        }
        finally
        {
            await tx.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        var tx = _transaction;
        if (tx is null) return;

        try
        {
            await tx.RollbackAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqlException)
        {
            // Сервер уже відкотив транзакцію (наприклад, THROW при XACT_ABORT ON) — нічого відкочувати.
        }
        finally
        {
            await tx.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        await BeginAsync(ct: ct);
        try
        {
            var result = await work();
            await CommitAsync(ct);
            return result;
        }
        catch
        {
            await RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task InTransactionAsync(Func<Task> work, CancellationToken ct = default) =>
        InTransactionAsync<bool>(async () =>
        {
            await work();
            return true;
        }, ct);

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await RollbackAsync(CancellationToken.None);   // незафіксована транзакція ніколи не «витікає»

        await _connection.DisposeAsync();
    }
}
