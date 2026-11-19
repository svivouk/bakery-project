using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;

namespace Platform.EventBus.Tracing;
public static class TracePropagation
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    public static string? CurrentTraceParent() => Activity.Current?.Id;
    public static PropagationContext Extract(IDictionary<string, object?>? headers) =>
    Propagator.Extract(default, headers, static (h, key) =>
        h is not null && h.TryGetValue(key, out var value)
            ? value switch
            {
                byte[] bytes => [Encoding.UTF8.GetString(bytes)],
                string text => [text],
                _ => []
            }
            : []);
    public static Activity? StartConsumerActivity(ActivitySource source, BasicDeliverEventArgs message, string queue)
    {
        var props = message.BasicProperties;
        var parent = Extract(props.Headers);
        if (parent.Baggage.Count > 0)
            Baggage.Current = parent.Baggage;

        var activity = source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", message.RoutingKey);
        activity?.SetTag("messaging.message.id", props.MessageId);
        activity?.SetTag("messaging.event.type", props.Type);
        return activity;
    }

    public static bool TryParse(string? traceParent, out ActivityContext context)
    {
        context = default;
        return !string.IsNullOrWhiteSpace(traceParent) && ActivityContext.TryParse(traceParent, null, out context);
    }
    public static void Inject(BasicProperties props, Activity? activity, string? fallbackTraceParent)
    {
        props.Headers ??= new Dictionary<string, object?>();

        var current = activity ?? Activity.Current;
        if (current is not null)
        {
            Propagator.Inject(
                new PropagationContext(current.Context, Baggage.Current),
                props.Headers,
                static (headers, key, value) => headers[key] = value);
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackTraceParent))
            props.Headers[MessageHeaders.TraceParent] = fallbackTraceParent;
    }
}
