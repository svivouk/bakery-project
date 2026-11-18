using Platform.EventBus.Events;
using Platform.EventBus.Outbox;
using Platform.EventBus.Serialization;
using System.Text.Json;

namespace Orders.Api.Messaging;

public sealed class OrdersOutboxMapper : IOutboxEventMapper
{
    public IntegrationEvent? Map(OutboxRecord r) => r.EventType switch
    {
        "OrderPlaced" => FromPayload(r, oldStatus: "", newStatus: "New"),
        "OrderStatusChanged" => FromPayload(r, oldStatus: null, newStatus: null),
        _ => null
    };

    private static OrderStatusChangedEvent FromPayload(OutboxRecord r, string? oldStatus, string? newStatus)
    {
        var p = JsonSerializer.Deserialize<Payload>(r.Payload, JsonDefaults.Options)
                ?? throw new InvalidOperationException($"Outbox {r.Id}: порожній payload {r.EventType}");

        return new OrderStatusChangedEvent(
            p.OrderId,
            p.CustomerId,
            oldStatus ?? p.OldStatus ?? "",
            newStatus ?? p.NewStatus ?? "",
            (p.Lines ?? []).Select(l => new OrderLineSnapshot(l.ProductId, l.VariantId, l.Quantity)).ToList())
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            CorrelationId = p.CorrelationId
        };
    }

    private sealed record Payload(Guid OrderId, Guid CustomerId, string? OldStatus, string? NewStatus, List<Line>? Lines, string? CorrelationId);
    private sealed record Line(Guid ProductId, Guid VariantId, int Quantity);
}


