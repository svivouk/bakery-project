using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Reviews.WebApi.Metrics;

public sealed class ReviewsMetrics
{
    public const string MeterName = "Reviews.Api";

    private readonly Counter<long> _created;
    private readonly Histogram<int> _rating;
    private readonly Histogram<double> _operationDuration;

    public ReviewsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>("reviews.created", unit: "{review}",
            description: "Кількість доданих відгуків");
        _rating = meter.CreateHistogram<int>("reviews.rating", unit: "{star}",
            description: "Розподіл оцінок у нових відгуках");
        _operationDuration = meter.CreateHistogram<double>("reviews.operation.duration", unit: "ms",
            description: "Тривалість операції з відгуком");
    }

    public void ReviewCreated(int rating)
    {
        _created.Add(1);
        _rating.Record(rating);
    }
    public void RecordDuration(string operation, bool success, double elapsedMs) =>
    _operationDuration.Record(elapsedMs,
        new KeyValuePair<string, object?>("operation", operation),   // place | confirm | cancel | get
        new KeyValuePair<string, object?>("success", success));
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var start = Stopwatch.GetTimestamp();
        var success = false;
        try
        {
            var result = await action();
            success = true;
            return result;
        }
        finally
        {
            RecordDuration(operation, success, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }
}