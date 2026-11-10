using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Clients;

// Джерела даних для композиції замовлення. Кожне має дві реалізації (HTTP і gRPC); яку використовувати,
// визначає Aggregator:Transport (grpc за замовчуванням), що дозволяє порівняти затримки двох транспортів.

public interface IOrdersSource
{
    /// <summary>Замовлення або null, якщо його немає.</summary>
    Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct);
}

public interface ICatalogSource
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}

public interface IReviewsSource
{
    Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}
