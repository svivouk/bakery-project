using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Catlog.Api.Metrics;

public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";
    private readonly Counter<long> _productViews;
    private readonly Counter<long> _productsChanged;
    private readonly Counter<long> _stockAdjustments;
    private readonly Histogram<double> _operationDuration;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _productViews = meter.CreateCounter<long>("catalog.product.views", unit: "{view}",
            description: "Перегляди товару (за протоколом)");
        _productsChanged = meter.CreateCounter<long>("catalog.product.changes", unit: "{change}",
            description: "Створення, оновлення, видалення товарів");
        _stockAdjustments = meter.CreateCounter<long>("catalog.stock.adjustments", unit: "{adjustment}",
            description: "Коригування залишків");
        _operationDuration = meter.CreateHistogram<double>("catalog.operation.duration", unit: "ms",
            description: "Тривалість операції з товаром");
    }

    public void ProductViewed(string protocol) =>
        _productViews.Add(1, new KeyValuePair<string, object?>("protocol", protocol));   // http | grpc

    public void ProductChanged(string operation) =>
        _productsChanged.Add(1, new KeyValuePair<string, object?>("operation", operation)); // created|updated|deleted

    public void StockAdjusted() => _stockAdjustments.Add(1);
    public void RecordDuration(string operation, bool success, double elapsedMs,
        string? cache = null, string? dbQuery = null)
    {
        var tags = new TagList
    {
        { "operation", operation },   // get_product | list_products | search
        { "success", success }
    };
        if (cache is not null) tags.Add("cache.result", cache);    // hit | miss
        if (dbQuery is not null) tags.Add("db.query", dbQuery);    // by_id | by_slug | list | search
        _operationDuration.Record(elapsedMs, tags);
    }
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