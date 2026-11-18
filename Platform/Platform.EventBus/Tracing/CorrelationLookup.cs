using System.Diagnostics;

namespace Platform.EventBus.Tracing;

public static class CorrelationLookup
{
    public const string TagName = "correlation_id";

    public static string? Current()
    {
        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            if (activity.GetTagItem(TagName) is string id && !string.IsNullOrWhiteSpace(id))
                return id;
        }

        return Activity.Current?.TraceId.ToString();
    }
}
