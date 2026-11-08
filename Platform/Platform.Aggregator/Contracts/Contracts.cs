namespace Platform.Aggregator.Contracts;

public sealed record OrderItemDto(
    Guid Id, Guid ProductId, Guid VariantId, string ProductName, string VariantName,
    decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id, long OrderNumber, Guid CustomerId, string Status, DateTime DesiredReadyAt,
    decimal Subtotal, decimal DiscountAmount, decimal DeliveryFee, decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);

public sealed record ProductDto(Guid ProductId, string Name, string Slug, string? ImageUrl, decimal BasePrice);

public sealed record ReviewDto(
    string Id, Guid ProductId, string ProductName, string AuthorName,
    int Rating, string? Text, int PhotosCount, DateTime CreatedAt);

public sealed record ReviewPageDto(IReadOnlyList<ReviewDto> Items, int Page, int PageSize, long TotalCount);

public sealed record FullOrderDto(
    OrderDto Order,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<string> Warnings);