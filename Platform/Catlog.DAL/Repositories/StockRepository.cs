using System.Data;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

/// <summary>
/// Залишки. Рядок StockItems змінюється з кожним замовленням, тому всі зміни — атомарні UPDATE з умовою
/// всередині збережених процедур; жодного «прочитав → змінив → записав» у C#.
/// </summary>
public sealed class StockRepository : BaseDapperRepository<StockItem>, IStockRepository
{
    public StockRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.StockItems", "VariantId", softDelete: false) { }

    protected override string InsertSql => @"
        INSERT INTO dbo.StockItems (VariantId, QuantityAvailable, DailyLimit)
        OUTPUT INSERTED.VariantId
        VALUES (@VariantId, @QuantityAvailable, @DailyLimit)";

    // Адміністративне виправлення залишку й добової норми (не шлях замовлення).
    protected override string UpdateSql => @"
        UPDATE dbo.StockItems
        SET QuantityAvailable = @QuantityAvailable, DailyLimit = @DailyLimit,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
        WHERE VariantId = @VariantId";

    protected override string DeleteSql => "DELETE FROM dbo.StockItems WHERE VariantId = @Id";

    public async Task ReserveAsync(Guid orderId, Guid variantId, int quantity, DateTime? expiresAtUtc,
        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_Stock_Reserve",
                new { OrderId = orderId, VariantId = variantId, Quantity = quantity, ExpiresAt = expiresAtUtc },
                Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number == CatalogSqlErrors.InsufficientStock)
        {
            throw new InsufficientStockException(variantId);
        }
        catch (SqlException ex) when (ex.Number == CatalogSqlErrors.VariantNotFound)
        {
            throw new NotFoundException("Variant", variantId);
        }
        catch (SqlException ex) when (ex.Number == CatalogSqlErrors.ReservationConflict)
        {
            throw new ConflictException(ex.Message, ex);
        }
        catch (SqlException ex) when (ex.Number == CatalogSqlErrors.InvalidQuantity)
        {
            throw new ValidationException(ex.Message);
        }
    }

    public async Task CommitAsync(Guid orderId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_Stock_Commit", new { OrderId = orderId },
                Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number == CatalogSqlErrors.ReservationNotFound)
        {
            throw new NotFoundException("Reservation", orderId);
        }
    }

    public async Task<int> ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleAsync<int>(new CommandDefinition(
            "dbo.usp_Stock_Release", new { OrderId = orderId },
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<int> ReleaseExpiredAsync(int batchSize, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleAsync<int>(new CommandDefinition(
            "dbo.usp_Stock_ReleaseExpired", new { BatchSize = batchSize },
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<int> ResetDailyAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleAsync<int>(new CommandDefinition(
            "dbo.usp_Stock_ResetDaily", transaction: Transaction,
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<StockReservation>> GetReservationsAsync(Guid orderId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ReservationId, VariantId, OrderId, Quantity, Status, CreatedAt, ExpiresAt
            FROM dbo.StockReservations
            WHERE OrderId = @OrderId
            ORDER BY CreatedAt";

        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<StockReservation>(
            new CommandDefinition(sql, new { OrderId = orderId }, Transaction, cancellationToken: ct));
        return rows.AsList();
    }
}
