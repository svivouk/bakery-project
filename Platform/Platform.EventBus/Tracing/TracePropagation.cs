using System.Diagnostics;
using RabbitMQ.Client;

namespace Platform.EventBus.Tracing;
public static class TracePropagation
{
    public static void Inject(BasicProperties props)
    {
        var activity = Activity.Current;
        if (activity?.Id is null) return;

        props.Headers ??= new Dictionary<string, object?>();
        props.Headers[MessageHeaders.TraceParent] = activity.Id;         
        if (!string.IsNullOrEmpty(activity.TraceStateString))
            props.Headers[MessageHeaders.TraceState] = activity.TraceStateString;
    }
}
