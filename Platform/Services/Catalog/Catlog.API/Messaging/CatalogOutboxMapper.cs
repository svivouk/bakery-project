using System.Text.Json;
using Platform.EventBus.Events;
using Platform.EventBus.Outbox;
using Platform.EventBus.Serialization;

namespace Catalog.Api.Messaging;

public sealed class CatalogOutboxMapper : IOutboxEventMapper
{
    public IntegrationEvent? Map(OutboxRecord r) => r.EventType switch
    {
        "ProductCreated" => Created(r),
        "PriceChanged" => Price(r),
        "ProductRenamed" => Renamed(r),
        "ProductDeactivated" => Deactivated(r),
        _ => null
    };

    private static ProductPriceChangedEvent Price(OutboxRecord r)
    {
        var p = Read<PricePayload>(r);
        return new ProductPriceChangedEvent(p.ProductId, p.VariantId, p.ProductName, p.OldPrice, p.NewPrice, p.VariantName)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            SourceVersion = r.Id,
            CorrelationId = p.CorrelationId,
            TraceParent = p.TraceParent
        };
    }

    private static ProductCreatedEvent Created(OutboxRecord r)
    {
        var p = Read<CreatedPayload>(r);
        return new ProductCreatedEvent(p.ProductId, p.Name, p.Slug, p.BasePrice)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            SourceVersion = r.Id,
            CorrelationId = p.CorrelationId,
            TraceParent = p.TraceParent
        };
    }

    private static ProductRenamedEvent Renamed(OutboxRecord r)
    {
        var p = Read<RenamedPayload>(r);
        return new ProductRenamedEvent(p.ProductId, p.OldName, p.NewName, p.Slug)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            SourceVersion = r.Id,
            CorrelationId = p.CorrelationId,
            TraceParent = p.TraceParent
        };
    }

    private static ProductDeletedEvent Deactivated(OutboxRecord r)
    {
        var p = Read<DeactivatedPayload>(r);
        return new ProductDeletedEvent(p.ProductId, p.ProductName)
        {
            EventId = r.EventId,
            OccurredAt = r.CreatedAtUtc,
            SourceVersion = r.Id,
            CorrelationId = p.CorrelationId,
            TraceParent = p.TraceParent
        };
    }

    private static T Read<T>(OutboxRecord r) =>
        JsonSerializer.Deserialize<T>(r.Payload, JsonDefaults.Options)
        ?? throw new InvalidOperationException($"Outbox {r.Id}: порожній payload {r.EventType}");

    private sealed record CreatedPayload(Guid ProductId, string Name, string Slug, decimal BasePrice, string? CorrelationId, string? TraceParent);
    private sealed record PricePayload(Guid ProductId, Guid? VariantId, string? ProductName, decimal? OldPrice, decimal NewPrice, string? VariantName, string? CorrelationId, string? TraceParent);
    private sealed record RenamedPayload(Guid ProductId, string OldName, string NewName, string Slug, string? CorrelationId, string? TraceParent);
    private sealed record DeactivatedPayload(Guid ProductId, string? ProductName, string? CorrelationId, string? TraceParent);
}
