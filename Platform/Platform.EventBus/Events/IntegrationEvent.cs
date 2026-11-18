namespace Platform.EventBus.Events;
// Базова подія. Незмінна (record + init) і самодостатня: споживач не має звертатися до сервісу-видавця по дані
public abstract record IntegrationEvent
{
    //Версія схеми повідомлення. Передається і в тілі, і в заголовку x-schema-version
    public int SchemaVersion { get; init; } = 1;

    //Ідентифікатор події: споживач за ним відкидає дублікати 
    public Guid EventId { get; init; } = Guid.NewGuid();

    //Момент, коли подія сталася (UTC).
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    //Наскрізний ідентифікатор запиту для трасування від HTTP-запиту до споживача
    public string? CorrelationId { get; init; }
}

//Куди публікувати подію: exchange і routing key. Publisher бере їх з атрибута, а не з рядків у коді
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventRouteAttribute(string exchange, string routingKey) : Attribute
{
    public string Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey;
}
