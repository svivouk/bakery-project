using RabbitMQ.Client;

namespace Platform.EventBus;
public static class MessageHeaders
{
    public const string CorrelationId = "x-correlation-id";
    public const string SchemaVersion = "x-schema-version";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
    public const string RetryCount = "x-retry-count";
    public const string LastError = "x-last-error";
    public static int GetInt(IReadOnlyBasicProperties props, string name)
    {
        if (props.Headers is null || !props.Headers.TryGetValue(name, out var value) || value is null)
            return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var n) => n,
            string text when int.TryParse(text, out var n) => n,
            _ => 0
        };
    }

    public static string? GetString(IReadOnlyBasicProperties props, string name)
    {
        if (props.Headers is null || !props.Headers.TryGetValue(name, out var value) || value is null)
            return null;

        return value switch
        {
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value.ToString()
        };
    }
}
