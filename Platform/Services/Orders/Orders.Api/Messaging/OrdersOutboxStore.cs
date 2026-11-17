using Microsoft.EntityFrameworkCore;
using Orders.Dal;
using Platform.EventBus.Outbox;

namespace Orders.Api.Messaging;

public sealed class OrdersOutboxStore(OrdersDbContext db) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxRecord>> GetPendingAsync(int batchSize, CancellationToken ct)
    {
        var rows = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        return rows.Select(m => new OutboxRecord(
            m.Id, m.EventId, m.EventType, m.AggregateId, m.Payload,
            DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc))).ToList();
    }

    public async Task MarkProcessedAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;

        var now = DateTime.UtcNow;
        await db.OutboxMessages
            .Where(m => ids.Contains(m.Id) && m.ProcessedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAt, now), ct);
    }
}
