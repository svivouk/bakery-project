using Microsoft.Extensions.Logging;
using Platform.EventBus.Consuming;
using Platform.EventBus.Events;
using Platform.EventBus.Serialization;
using Platform.EventBus.Tracing;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Platform.EventBus.Publishing;

public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    private static readonly ActivitySource Source = new(EventBusTelemetry.SourceName);

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

        using var activity = StartProducerActivity(@event, exchange, routingKey);
        var correlationId = @event.CorrelationId ?? CorrelationLookup.Current();

        var props = new BasicProperties
        {
            Persistent = true,                                 
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
        activity?.SetTag("correlation_id", correlationId);
        TracePropagation.Inject(props, activity, @event.TraceParent);

        byte[] body;
        if (@event.CorrelationId is null && correlationId is not null)
        {
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
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to publish {EventType} {EventId} to {Exchange}/{RoutingKey}",
                eventType.Name, @event.EventId, exchange, routingKey);
            throw;                                             
        }

        logger.LogInformation(
            "Published {EventType} {EventId} (schema v{SchemaVersion}, correlation {CorrelationId}) to {Exchange}/{RoutingKey}",
            eventType.Name, @event.EventId, @event.SchemaVersion, correlationId, exchange, routingKey);
    }
    private static Activity? StartProducerActivity(IntegrationEvent @event, string exchange, string routingKey)
    {
        var name = $"{exchange} publish";
        var activity = Activity.Current is null && TracePropagation.TryParse(@event.TraceParent, out var parent)
            ? Source.StartActivity(name, ActivityKind.Producer, parent)
            : Source.StartActivity(name, ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", @event.EventId.ToString());
        return activity;
    }
}
