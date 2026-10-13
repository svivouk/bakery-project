using Catalog.Domain.Constants;

namespace Catalog.Domain.Models;

/// Рядок view dbo.vw_ProductListing — денормалізована «картка товару» для списків.
public class ProductListingItem
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? ImageUrl { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string CategorySlug { get; set; } = "";
    public decimal BasePrice { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int VariantCount { get; set; }
    public int TotalAvailable { get; set; }
    public bool InStock { get; set; }
    public decimal AvgRating { get; set; }
    public int ReviewCount { get; set; }

    /// Назви алергенів через кому (Presence = 1) або null
    public string? Allergens { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ProductListingFilter
{
    public string? CategorySlug { get; init; }
    public string? Search { get; init; }
    public bool InStockOnly { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public ProductSort Sort { get; init; } = ProductSort.Newest;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);

/// Запис Outbox (подія, записана в одній транзакції зі зміною даних).
public class OutboxMessage
{
    public long OutboxId { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
