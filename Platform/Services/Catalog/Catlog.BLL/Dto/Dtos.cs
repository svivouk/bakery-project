using System.ComponentModel.DataAnnotations;
using Catalog.Domain.Constants;

namespace Catalog.Bll.Dto;

// Валідація двох рівнів:
//  1) тут, на DTO — DataAnnotations (обов'язковість, довжина, формат, діапазон). Некоректний запит
//     відхиляється атрибутом [ApiController] (400 ProblemDetails) ще до виклику сервісу;
//  2) у BLL (Check + перевірки в сервісах) — правила, яким потрібен стан БД або кілька полів одразу
//     (унікальність slug, існування категорії, RowVersion/конкурентність, ліміти позицій резервування).
//
// У позиційних record-ах атрибути стоять на параметрах (без префікса property:). MVC читає саме їх;
// атрибут з property: на record кидає InvalidOperationException при першому запиті.

// ---------- Категорії ----------

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description, int SortOrder, bool IsActive);

public sealed record CreateCategoryRequest(
    [Required(ErrorMessage = Msg.Required), StringLength(100, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), StringLength(120, ErrorMessage = Msg.MaxLength),
     RegularExpression(Msg.SlugPattern, ErrorMessage = Msg.Slug)] string Slug,
    [StringLength(500, ErrorMessage = Msg.MaxLength)] string? Description,
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    [Required(ErrorMessage = Msg.Required), StringLength(100, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), StringLength(120, ErrorMessage = Msg.MaxLength),
     RegularExpression(Msg.SlugPattern, ErrorMessage = Msg.Slug)] string Slug,
    [StringLength(500, ErrorMessage = Msg.MaxLength)] string? Description,
    int SortOrder,
    bool IsActive);

public sealed class ListingQuery
{
    [StringLength(120, ErrorMessage = Msg.MaxLength)]
    public string? CategorySlug { get; init; }

    [StringLength(100, ErrorMessage = Msg.MaxLength)]
    public string? Search { get; init; }

    public bool InStockOnly { get; init; }

    [Range(0, Msg.MaxPrice, ErrorMessage = Msg.Range)]
    public decimal? MinPrice { get; init; }

    [Range(0, Msg.MaxPrice, ErrorMessage = Msg.Range)]
    public decimal? MaxPrice { get; init; }

    public ProductSort Sort { get; init; } = ProductSort.Newest;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record ProductListItemDto(
    Guid ProductId, string Name, string Slug, string? ImageUrl,
    Guid CategoryId, string CategoryName, string CategorySlug,
    decimal BasePrice, decimal? MinPrice, decimal? MaxPrice, int VariantCount,
    int TotalAvailable, bool InStock, decimal AvgRating, int ReviewCount, string? Allergens);

public sealed record VariantInfoDto(Guid VariantId, string Name, string Sku, decimal Price, int? WeightGrams, int? Servings);

public sealed record AllergenDto(string Code, string Name, string Presence);

public sealed record ProductDetailsDto(
    Guid ProductId, string Name, string Slug, string? Description, string? ImageUrl,
    decimal BasePrice, int? WeightGrams, int? ShelfLifeHours,
    IReadOnlyList<VariantInfoDto> Variants, IReadOnlyList<string> Ingredients, IReadOnlyList<AllergenDto> Allergens);

public sealed record VariantDto(
    Guid VariantId, Guid ProductId, string Name, string Sku, decimal Price,
    int? WeightGrams, int? Servings, int SortOrder, bool IsActive, byte[] RowVersion);

public sealed record ProductDto(
    Guid ProductId, Guid CategoryId, string Name, string Slug, string? Description, string? ImageUrl,
    decimal BasePrice, int? WeightGrams, int? ShelfLifeHours, bool IsActive, byte[] RowVersion,
    IReadOnlyList<VariantDto> Variants);

public sealed record AllergenLink(Guid AllergenId, AllergenPresence Presence);

public sealed record CreateVariantRequest(
    [Required(ErrorMessage = Msg.Required), StringLength(100, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), StringLength(40, ErrorMessage = Msg.MaxLength)] string Sku,
    [Range(0, Msg.MaxPrice, ErrorMessage = Msg.Range)] decimal Price,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? WeightGrams,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? Servings,
    int SortOrder = 0,
    [Range(0, int.MaxValue, ErrorMessage = Msg.Range)] int InitialQuantity = 0,
    [Range(0, int.MaxValue, ErrorMessage = Msg.Range)] int DailyLimit = 0);

public sealed record UpdateVariantRequest(
    [Required(ErrorMessage = Msg.Required), StringLength(100, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), StringLength(40, ErrorMessage = Msg.MaxLength)] string Sku,
    [Range(0, Msg.MaxPrice, ErrorMessage = Msg.Range)] decimal Price,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? WeightGrams,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? Servings,
    int SortOrder,
    bool IsActive,
    [Required(ErrorMessage = Msg.RowVersion), MinLength(8, ErrorMessage = Msg.RowVersion),
     MaxLength(8, ErrorMessage = Msg.RowVersion)] byte[] RowVersion);

public sealed record CreateProductRequest(
    Guid CategoryId,
    [Required(ErrorMessage = Msg.Required), StringLength(200, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), StringLength(220, ErrorMessage = Msg.MaxLength),
     RegularExpression(Msg.SlugPattern, ErrorMessage = Msg.Slug)] string Slug,
    [StringLength(2000, ErrorMessage = Msg.MaxLength)] string? Description,
    [StringLength(500, ErrorMessage = Msg.MaxLength)] string? ImageUrl,
    [Range(0, Msg.MaxPrice, ErrorMessage = Msg.Range)] decimal BasePrice,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? WeightGrams,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? ShelfLifeHours,
    IReadOnlyList<Guid>? IngredientIds,
    IReadOnlyList<AllergenLink>? Allergens,
    IReadOnlyList<CreateVariantRequest>? Variants);

public sealed record UpdateProductRequest(
    Guid CategoryId,
    [Required(ErrorMessage = Msg.Required), StringLength(200, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), StringLength(220, ErrorMessage = Msg.MaxLength),
     RegularExpression(Msg.SlugPattern, ErrorMessage = Msg.Slug)] string Slug,
    [StringLength(2000, ErrorMessage = Msg.MaxLength)] string? Description,
    [StringLength(500, ErrorMessage = Msg.MaxLength)] string? ImageUrl,
    [Range(0, Msg.MaxPrice, ErrorMessage = Msg.Range)] decimal BasePrice,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? WeightGrams,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int? ShelfLifeHours,
    bool IsActive,
    [Required(ErrorMessage = Msg.RowVersion), MinLength(8, ErrorMessage = Msg.RowVersion),
     MaxLength(8, ErrorMessage = Msg.RowVersion)] byte[] RowVersion,
    IReadOnlyList<Guid>? IngredientIds,
    IReadOnlyList<AllergenLink>? Allergens);

public sealed record IngredientDto(Guid Id, string Name);

public sealed record AllergenRefDto(Guid Id, string Code, string Name);

public sealed record RatingSummaryChangedEvent(
    Guid EventId,
    Guid ProductId,
    [Range(0, 5, ErrorMessage = Msg.Range)] decimal AvgRating,
    [Range(0, int.MaxValue, ErrorMessage = Msg.Range)] int ReviewCount,
    long Version);


public sealed record StockDto(Guid VariantId, int QuantityAvailable, int DailyLimit);

public sealed record AdjustStockRequest(
    [Range(0, int.MaxValue, ErrorMessage = Msg.Range)] int QuantityAvailable,
    [Range(0, int.MaxValue, ErrorMessage = Msg.Range)] int DailyLimit);

public sealed record ReserveLine(
    Guid VariantId,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int Quantity);

public sealed record ReserveRequest(
    Guid OrderId,
    [Required(ErrorMessage = Msg.Required)] IReadOnlyList<ReserveLine> Lines,
    int? TtlMinutes = null);

public sealed record ReservationDto(Guid OrderId, DateTime ExpiresAtUtc, IReadOnlyList<ReserveLine> Lines);
