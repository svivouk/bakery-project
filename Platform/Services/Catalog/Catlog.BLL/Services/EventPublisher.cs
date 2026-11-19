using System.Text.Json;
using Catalog.Dal.Interfaces;
using Platform.EventBus.Tracing;

namespace Catalog.Bll.Services;
internal static class EventPublisher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Task PublishAsync(IUnitOfWork uow, string eventType, Guid aggregateId, object payload,
        CancellationToken ct)
    {
        var node = JsonSerializer.SerializeToNode(payload, Json)!.AsObject();
        node["correlationId"] = CorrelationLookup.Current();
        node["traceParent"] = TracePropagation.CurrentTraceParent();
        return uow.Outbox.AddAsync(eventType, aggregateId, node.ToJsonString(Json), ct);
    }
}
