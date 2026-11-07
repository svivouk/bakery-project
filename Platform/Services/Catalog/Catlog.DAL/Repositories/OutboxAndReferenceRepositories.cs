using Catalog.Dal.Interfaces;
using Catalog.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

public sealed class OutboxRepository : RepositoryBase, IOutboxRepository
{
    public OutboxRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<Guid> AddAsync(string eventType, Guid aggregateId, string payloadJson,
        CancellationToken ct = default)
    {
        // Подія й зміна даних — в одній транзакції (інакше втрачається сенс Outbox).
        RequireTransaction();
        await EnsureOpenAsync(ct);

        var eventId = Guid.NewGuid();
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO dbo.OutboxMessages (EventId, EventType, AggregateId, Payload)
            VALUES (@EventId, @EventType, @AggregateId, @Payload)",
            new
            {
                EventId = eventId,
                EventType = eventType,
                AggregateId = aggregateId,
                Payload = payloadJson
            },
            Transaction, cancellationToken: ct));
        return eventId;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize,
        CancellationToken ct = default)
    {
        // Обслуговується IX_Outbox_Unprocessed (ProcessedAt IS NULL).
        const string sql = @"
            SELECT TOP (@BatchSize) OutboxId, EventId, EventType, AggregateId, Payload, CreatedAt, ProcessedAt
            FROM dbo.OutboxMessages
            WHERE ProcessedAt IS NULL
            ORDER BY OutboxId";

        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<OutboxMessage>(
            new CommandDefinition(sql, new { BatchSize = batchSize }, Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task MarkProcessedAsync(IReadOnlyCollection<long> outboxIds, CancellationToken ct = default)
    {
        if (outboxIds.Count == 0) return;

        await EnsureOpenAsync(ct);
        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE dbo.OutboxMessages
            SET ProcessedAt = SYSUTCDATETIME()
            WHERE OutboxId IN @Ids AND ProcessedAt IS NULL",
            new { Ids = outboxIds }, Transaction, cancellationToken: ct));
    }
}

public sealed class ReferenceRepository : RepositoryBase, IReferenceRepository
{
    public ReferenceRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Ingredient>(new CommandDefinition(
            "SELECT IngredientId, Name FROM dbo.Ingredients WHERE IsDeleted = 0 ORDER BY Name",
            transaction: Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Allergen>> GetAllergensAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Allergen>(new CommandDefinition(
            "SELECT AllergenId, Code, Name FROM dbo.Allergens ORDER BY Name",
            transaction: Transaction, cancellationToken: ct));
        return rows.AsList();
    }
}
