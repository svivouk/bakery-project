using System.Globalization;
using Grpc.Core;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Contracts;
using Orders.Grpc;
using Reviews.Grpc;

namespace Platform.Aggregator.GrpcClients;
public sealed class OrdersGrpcSource(OrdersGrpc.OrdersGrpcClient client, ILogger<OrdersGrpcSource> logger) : IOrdersSource
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct)
    {
        try
        {
            using var call = client.GetOrderAsync(new GetOrderRequest { OrderId = id.ToString() },
                deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);
            var o = await call.ResponseAsync;

            return new OrderDto(
                Guid.Parse(o.OrderId), o.OrderNumber, Guid.Parse(o.CustomerId), o.Status,
                o.DesiredReadyAt.ToDateTime(),
                Dec(o.Subtotal), Dec(o.DiscountAmount), Dec(o.DeliveryFee), Dec(o.TotalAmount),
                o.Items.Select(i => new OrderItemDto(
                    Guid.Parse(i.ItemId), Guid.Parse(i.ProductId), Guid.Parse(i.VariantId),
                    i.ProductName, i.VariantName, Dec(i.UnitPrice), i.Quantity, Dec(i.LineTotal))).ToList());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("Orders gRPC GetOrder: NotFound ({Detail})", ex.Status.Detail);
            return null;
        }
    }

    private static decimal Dec(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
public sealed class CatalogGrpcSource(CatalogGrpcGateway gateway) : ICatalogSource
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];

        var result = await gateway.GetProductsByIdsAsync(idList, ct);
        return result.Value!.Products.Select(p => new ProductDto(
            Guid.Parse(p.ProductId), p.Name, p.Slug,
            string.IsNullOrEmpty(p.ImageUrl) ? null : p.ImageUrl,
            decimal.Parse(p.BasePrice, NumberStyles.Number, CultureInfo.InvariantCulture))).ToList();
    }
}
public sealed class ReviewsGrpcSource(ReviewsGrpc.ReviewsGrpcClient client) : IReviewsSource
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var request = new GetReviewsForProductsRequest { PerProductLimit = 50 };   
        request.ProductIds.AddRange(ids.Distinct().Select(i => i.ToString()));
        if (request.ProductIds.Count == 0) return [];

        using var call = client.GetReviewsForProductsAsync(request,
            deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);
        var reply = await call.ResponseAsync;

        return reply.Products.SelectMany(p => p.Reviews).Select(r => new ReviewDto(
            r.ReviewId, Guid.Parse(r.ProductId), r.ProductName, r.AuthorName, r.Rating,
            r.HasText ? r.Text : null, r.PhotosCount, r.CreatedAt.ToDateTime())).ToList();
    }
}
