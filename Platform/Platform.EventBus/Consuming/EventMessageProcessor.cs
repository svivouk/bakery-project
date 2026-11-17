using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using Platform.EventBus.Serialization;
using Platform.EventBus.Tracing;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Platform.EventBus.Consuming;


public enum MessageOutcome
{
    Ack,
    Reject,
    Requeue,
    Retry
}

public readonly record struct ProcessingResult(MessageOutcome Outcome, Exception? Error = null)
{
    public static implicit operator ProcessingResult(MessageOutcome outcome) => new(outcome);
    public static ProcessingResult Retry(Exception error) => new(MessageOutcome.Retry, error);
}

public static class EventBusTelemetry
{
    public const string SourceName = "Platform.Messaging";
}
public sealed class EventMessageProcessor(
    IServiceScopeFactory scopes,
    EventHandlerRegistry registry,
    ILogger<EventMessageProcessor> logger)
{
    public const int MaxSupportedSchemaVersion = 1;

    private static readonly ActivitySource Source = new(EventBusTelemetry.SourceName);

    private const int BodyPreviewLength = 500;


    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> EntityIdProperties = new();

    public async Task<ProcessingResult> ProcessAsync(BasicDeliverEventArgs message, string queue, CancellationToken ct)
    {
        var props = message.BasicProperties;
        var eventType = props.Type;
        var correlationId = MessageHeaders.GetString(props, MessageHeaders.CorrelationId);

        // Контекст, який додається до КОЖНОГО логу цього повідомлення (в Dashboard його можна фільтрувати).
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Operation"] = "ConsumeMessage",
            ["EventType"] = eventType,
            ["Queue"] = queue,
            ["RoutingKey"] = message.RoutingKey,
            ["MessageId"] = props.MessageId,
            ["CorrelationId"] = correlationId,
            ["RetryCount"] = MessageHeaders.GetInt(props, MessageHeaders.RetryCount)
        });

        using var activity = TracePropagation.StartConsumerActivity(Source, message, queue);
        activity?.SetTag("correlation_id", correlationId);

        if (string.IsNullOrWhiteSpace(eventType))
        {
            logger.LogError("Message {MessageId} on {Queue} has no type, sent to DLQ", props.MessageId, queue);
            return MessageOutcome.Reject;
        }

        var schemaText = MessageHeaders.GetString(props, MessageHeaders.SchemaVersion);
        if (int.TryParse(schemaText, out var schemaVersion) && schemaVersion > MaxSupportedSchemaVersion)
        {
            logger.LogError(
                "Message {MessageId} {EventType} has schema v{SchemaVersion}, newest supported is v{Max}: sent to DLQ",
                props.MessageId, eventType, schemaVersion, MaxSupportedSchemaVersion);
            return MessageOutcome.Reject;
        }

        if (!registry.TryGet(eventType, out var registration))
        {
            logger.LogWarning("No handler for {EventType} (message {MessageId}), acknowledged without processing",
                eventType, props.MessageId);
            return MessageOutcome.Ack;
        }

        IntegrationEvent @event;
        try
        {
            @event = JsonSerializer.Deserialize(message.Body.Span, registration.EventType, JsonDefaults.Options)
                         as IntegrationEvent
                     ?? throw new JsonException("Тіло повідомлення порожнє.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Cannot deserialize {EventType} (message {MessageId}) on {Queue}, sent to DLQ. Body starts with: {BodyPreview}",
                eventType, props.MessageId, queue, BodyPreview(message.Body.Span));
            return MessageOutcome.Reject;
        }

        var eventScope = new Dictionary<string, object?> { ["EventId"] = @event.EventId };
        foreach (var (name, value) in EntityIds(@event)) eventScope[name] = value;
        using var eventLogScope = logger.BeginScope(eventScope);

        logger.LogInformation(
            "Received {EventType} {EventId} (v{SchemaVersion}, source version {SourceVersion}, routing key {RoutingKey})",
            eventType, @event.EventId, @event.SchemaVersion, @event.SourceVersion, message.RoutingKey);

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await registration.Invoke(scope.ServiceProvider, @event, ct);
            logger.LogInformation("Handled {EventType} {EventId}", eventType, @event.EventId);
            return MessageOutcome.Ack;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return MessageOutcome.Requeue;                      
        }
        catch (Exception ex) when (ex is PermanentFailureException or JsonException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Permanent failure for {EventType} {EventId}: {Reason}. Sent to DLQ",
                eventType, @event.EventId, ex.Message);
            return MessageOutcome.Reject;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Transient failure while handling {EventType} {EventId}: {Reason}",
                eventType, @event.EventId, ex.Message);
            return ProcessingResult.Retry(ex);
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EntityIds(IntegrationEvent @event)
    {
        var properties = EntityIdProperties.GetOrAdd(@event.GetType(), t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal)
                        && p.Name != nameof(IntegrationEvent.EventId)
                        && (p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?)
                            || p.PropertyType == typeof(long) || p.PropertyType == typeof(int)))
            .ToArray());

        foreach (var property in properties)
            if (property.GetValue(@event) is { } value)
                yield return new(property.Name, value);
    }

    private static string BodyPreview(ReadOnlySpan<byte> body)
    {
        var slice = body.Length > BodyPreviewLength ? body[..BodyPreviewLength] : body;
        return Encoding.UTF8.GetString(slice);
    }
}
