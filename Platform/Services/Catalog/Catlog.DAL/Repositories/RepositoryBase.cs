using System.Data;
using Catalog.Domain.Exceptions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

public static class CatalogSqlErrors
{
    public const int InsufficientStock = 50001;
    public const int VariantNotFound = 50002;
    public const int ReservationConflict = 50003;
    public const int InvalidQuantity = 50004;
    public const int ReservationNotFound = 50005;
}

public abstract class RepositoryBase
{
    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }
    protected SqlTransaction RequireTransaction() =>
        Transaction ?? throw new InvalidOperationException(
            "Операція потребує активної транзакції Unit of Work (виклич IUnitOfWork.BeginAsync / InTransactionAsync).");
    protected static DbString Ansi(string value, int length) =>
        new() { Value = value, IsAnsi = true, Length = length };
    protected static async Task<T> GuardAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new ConflictException("Запис із такими унікальними значеннями (назва, slug або SKU) уже існує.", ex);
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            throw new ConflictException(
                "Операція порушує обмеження цілісності (неіснуюче посилання або недопустиме значення).", ex);
        }
    }
}
