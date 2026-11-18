using Platform.EventBus.Events;
using Platform.EventBus.Outbox;
using Platform.EventBus.Serialization;
using System.Text.Json;

namespace Catalog.API.Messaging;

public sealed class CatalogOutboxMapper : IOutboxEventMapper
{
    public IntegrationEvent? Map(OutboxRecord r) => r.EventType switch
    {
        "PriceChanged" => Price(r),
        "ProductRenamed" => Renamed(r),
        "ProductDeactivated" => Deactivated(r),
        _ => null
    };

    private static ProductPriceChangedEvent Price(OutboxRecord r)
    {
        var p = Read<PricePayload>(r);
        return new ProductPriceChangedEvent(p.ProductId, p.VariantId, p.ProductName, p.OldPrice, p.NewPrice)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            CorrelationId = p.CorrelationId
        };
    }

    private static ProductRenamedEvent Renamed(OutboxRecord r)
    {
        var p = Read<RenamedPayload>(r);
        return new ProductRenamedEvent(p.ProductId, p.OldName, p.NewName, p.Slug)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            CorrelationId = p.CorrelationId
        };
    }

    private static ProductDeletedEvent Deactivated(OutboxRecord r)
    {
        var p = Read<DeactivatedPayload>(r);
        return new ProductDeletedEvent(p.ProductId, p.ProductName)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            CorrelationId = p.CorrelationId
        };
    }

    private static T Read<T>(OutboxRecord r) =>
        JsonSerializer.Deserialize<T>(r.Payload, JsonDefaults.Options)
        ?? throw new InvalidOperationException($"Outbox {r.Id}: порожній payload {r.EventType}");

    private sealed record PricePayload(Guid ProductId, Guid? VariantId, string? ProductName, decimal? OldPrice, decimal NewPrice, string? CorrelationId);
    private sealed record RenamedPayload(Guid ProductId, string OldName, string NewName, string Slug, string? CorrelationId);
    private sealed record DeactivatedPayload(Guid ProductId, string? ProductName, string? CorrelationId);
}
