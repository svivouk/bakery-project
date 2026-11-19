namespace Platform.EventBus.Consuming;
public sealed class PermanentFailureException(string message, Exception? inner = null) : Exception(message, inner);
