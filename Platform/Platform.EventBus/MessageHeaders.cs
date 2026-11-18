namespace Platform.EventBus;
public static class MessageHeaders
{
    public const string CorrelationId = "x-correlation-id";
    public const string SchemaVersion = "x-schema-version";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
}
