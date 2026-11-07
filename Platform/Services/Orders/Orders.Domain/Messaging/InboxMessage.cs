using Orders.Domain;

namespace Orders.Domain.Messaging;

/// Запис про оброблену вхідну подію: ключ EventId робить обробку ідемпотентною
/// (повторна вставка порушить PK, і споживач пропустить дубль)
public sealed class InboxMessage
{
    private InboxMessage() { } 

    public InboxMessage(Guid eventId, string eventType)
    {
        EventId = Guard.NotEmpty(eventId, "Подія");
        EventType = Guard.Required(eventType, "Тип події", OutboxMessage.EventTypeMaxLength);
        ProcessedAt = DateTime.UtcNow;
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public DateTime ProcessedAt { get; private set; }
}
