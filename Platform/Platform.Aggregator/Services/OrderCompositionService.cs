using System.Diagnostics;
using Grpc.Core;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Services;

public sealed class OrderCompositionService(
    OrdersClient orders, CatalogClient catalog, ReviewsClient reviews,
    ILogger<OrderCompositionService> logger)
{
    public async Task<FullOrderDto?> GetFullOrderAsync(Guid orderId, CancellationToken ct)
    {
        var warnings = new List<string>();

        logger.LogInformation("Calling orders-api for order {OrderId}", orderId);
        var sw = Stopwatch.StartNew();
        var order = await orders.GetOrderAsync(orderId, ct);
        logger.LogInformation("orders-api responded in {Elapsed} ms", sw.ElapsedMilliseconds);
        if (order is null) return null;

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();

        // Незалежні виклики паралельно: Каталог (gRPC) і Відгуки (HTTP, доки в Reviews немає gRPC).
        var productsTask = SafeAsync(() => catalog.GetProductsAsync(productIds, ct), "catalog-api", warnings, ct);
        var reviewsTask = SafeAsync(() => reviews.GetForProductsAsync(productIds, ct), "reviews-api", warnings, ct);
        await Task.WhenAll(productsTask, reviewsTask);

        // узгодженість: у відповіді лише товари й відгуки, що належать цьому замовленню
        var allowed = productIds.ToHashSet();
        var products = (await productsTask ?? []).Where(p => allowed.Contains(p.ProductId)).ToList();
        var productReviews = (await reviewsTask ?? []).Where(r => allowed.Contains(r.ProductId)).ToList();

        foreach (var missing in productIds.Except(products.Select(p => p.ProductId)))
            if (!warnings.Contains("catalog-api is unavailable"))
                warnings.Add($"Товар {missing} не знайдено в каталозі");

        return new FullOrderDto(order, products, productReviews, warnings);
    }

    private async Task<T?> SafeAsync<T>(Func<Task<T>> call, string service, List<string> warnings, CancellationToken ct)
    {
        logger.LogInformation("Calling {Service}", service);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)
        {
            // gRPC: сервіс недоступний, дедлайн вичерпано або інша помилка статусу, у будь-якому разі часткова відповідь.
            logger.LogWarning(ex, "{Service} gRPC failed with {StatusCode} after {Elapsed} ms",
                service, ex.StatusCode, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable after {Elapsed} ms", service, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}
