using Platform.EventBus.Events;

namespace Platform.EventBus.Outbox;

public sealed record OutboxRecord(
    long Id,
    Guid EventId,
    string EventType,
    Guid AggregateId,
    string Payload,
    DateTime CreatedAtUtc);
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxRecord>> GetPendingAsync(int batchSize, CancellationToken ct);
    Task MarkProcessedAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
}
public interface IOutboxEventMapper
{
    IntegrationEvent? Map(OutboxRecord record);
}
