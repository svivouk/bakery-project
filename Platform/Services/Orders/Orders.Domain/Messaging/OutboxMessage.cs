using System.Text.Json;
using Orders.Domain;

namespace Orders.Domain.Messaging;

/// Вихідна подія. Додається в ту саму транзакцію, що й зміна даних, а фонова служба
/// відправляє її й викликає <see cref="MarkProcessed"/>
public sealed class OutboxMessage
{
    public const int EventTypeMaxLength = 100;

    private OutboxMessage() { } 

    public OutboxMessage(string eventType, Guid aggregateId, string payload)
    {
        EventId = Guid.NewGuid();
        EventType = Guard.Required(eventType, "Тип події", EventTypeMaxLength);
        AggregateId = Guard.NotEmpty(aggregateId, "Агрегат");
        Payload = ValidateJson(payload);
        CreatedAt = DateTime.UtcNow;
    }

    public long Id { get; private set; }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    public void MarkProcessed()
    {
        ProcessedAt ??= DateTime.UtcNow;
    }
    private static string ValidateJson(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainException("Payload: значення обов'язкове.");
        try
        {
            using var _ = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            throw new DomainException("Payload: очікується коректний JSON.");
        }
        return payload;
    }
}
