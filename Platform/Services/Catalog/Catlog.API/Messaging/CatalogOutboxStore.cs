using Catalog.Dal.Interfaces;
using Platform.EventBus.Outbox;

namespace Catalog.Api.Messaging;
public sealed class CatalogOutboxStore(IUnitOfWork uow) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxRecord>> GetPendingAsync(int batchSize, CancellationToken ct)
    {
        var rows = await uow.Outbox.GetUnprocessedAsync(batchSize, ct);
        return rows.Select(r => new OutboxRecord(
            r.OutboxId, r.EventId, r.EventType, r.AggregateId, r.Payload,
            DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc))).ToList();
    }

    public Task MarkProcessedAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
        uow.Outbox.MarkProcessedAsync(ids, ct);
}
