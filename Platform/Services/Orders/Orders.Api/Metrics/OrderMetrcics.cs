using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orders.Api.Metrics;

public sealed class OrdersMetrics
{
    public const string MeterName = "Orders.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _statusChanged;
    private readonly Histogram<double> _operationDuration;
    private int _inFlight;

    public OrdersMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge("orders.operations.in_flight",
            () => Volatile.Read(ref _inFlight),
            unit: "{operation}", description: "Операції з замовленнями, що виконуються просто зараз");

        _created = meter.CreateCounter<long>("orders.created", unit: "{order}",
            description: "Кількість створених замовлень");
        _failed = meter.CreateCounter<long>("orders.failed", unit: "{order}",
            description: "Кількість невдалих спроб створення замовлення");
        _statusChanged = meter.CreateCounter<long>("orders.status_changed", unit: "{change}",
            description: "Кількість змін статусу замовлення");
        _operationDuration = meter.CreateHistogram<double>("orders.operation.duration", unit: "ms",
            description: "Тривалість операції з замовленням");

    }

    public void OrderCreated(string status, string paymentMethod) =>
        _created.Add(1,
            new KeyValuePair<string, object?>("order.status", status),        
            new KeyValuePair<string, object?>("payment.method", paymentMethod));

    public void OrderFailed(string reason) =>
        _failed.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void StatusChanged(string from, string to) =>
        _statusChanged.Add(1,
            new KeyValuePair<string, object?>("status.from", from),
            new KeyValuePair<string, object?>("status.to", to));
    public void RecordDuration(string operation, bool success, double elapsedMs) =>
    _operationDuration.Record(elapsedMs,
        new KeyValuePair<string, object?>("operation", operation),   // place | confirm | cancel | get
        new KeyValuePair<string, object?>("success", success));
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var start = Stopwatch.GetTimestamp();
        var success = false;
        Interlocked.Increment(ref _inFlight);
        try
        {
            var result = await action();
            success = true;
            return result;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
            RecordDuration(operation, success, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }
}