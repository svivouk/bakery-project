using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using Platform.EventBus.Serialization;
using Platform.EventBus.Tracing;
using RabbitMQ.Client;

namespace Platform.EventBus.Publishing;

public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent
    {
        var route = @event.GetType().GetCustomAttribute<EventRouteAttribute>()
            ?? throw new InvalidOperationException(
                $"{@event.GetType().Name} не має [EventRoute]: вкажіть exchange і routing key явно.");

        return PublishAsync(@event, route.Exchange, route.RoutingKey, ct);
    }

    public async Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
    {
        var eventType = @event.GetType();

        // Якщо подію створили без CorrelationId, беремо його з поточного запиту (або TraceId).
        var correlationId = @event.CorrelationId ?? CorrelationLookup.Current();

        var props = new BasicProperties
        {
            Persistent = true,                                  // delivery mode 2: переживає перезапуск брокера (черга теж durable)
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = @event.EventId.ToString(),
            Type = eventType.Name,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                [MessageHeaders.SchemaVersion] = @event.SchemaVersion
            }
        };
        if (correlationId is not null)
            props.Headers[MessageHeaders.CorrelationId] = correlationId;
        TracePropagation.Inject(props);

        // Серіалізуємо за фактичним типом, інакше при T = IntegrationEvent втратяться поля похідного запису.
        byte[] body;
        if (@event.CorrelationId is null && correlationId is not null)
        {
            // Подію створили без CorrelationId: додаємо його в тіло, щоб воно збігалося із заголовком.
            var node = JsonSerializer.SerializeToNode(@event, eventType, JsonDefaults.Options)!.AsObject();
            node["correlationId"] = correlationId;
            body = JsonSerializer.SerializeToUtf8Bytes(node, JsonDefaults.Options);
        }
        else
        {
            body = JsonSerializer.SerializeToUtf8Bytes(@event, eventType, JsonDefaults.Options);
        }

        try
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, props, body, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to publish {EventType} {EventId} to {Exchange}/{RoutingKey}",
                eventType.Name, @event.EventId, exchange, routingKey);
            throw;                                              // рішення про повтор приймає викликач
        }

        logger.LogInformation(
            "Published {EventType} {EventId} (schema v{SchemaVersion}, correlation {CorrelationId}) to {Exchange}/{RoutingKey}",
            eventType.Name, @event.EventId, @event.SchemaVersion, correlationId, exchange, routingKey);
    }
}
