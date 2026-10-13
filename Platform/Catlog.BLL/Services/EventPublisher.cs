using System.Text.Json;
using Catalog.Dal.Interfaces;

namespace Catalog.Bll.Services;
internal static class EventPublisher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Task PublishAsync(IUnitOfWork uow, string eventType, Guid aggregateId, object payload,
        CancellationToken ct) =>
        uow.Outbox.AddAsync(eventType, aggregateId, JsonSerializer.Serialize(payload, Json), ct);
}
