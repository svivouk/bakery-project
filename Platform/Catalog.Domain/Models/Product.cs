using Catalog.Domain.Constants;

namespace Catalog.Domain.Models;

public class Product : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal BasePrice { get; set; }
    public int? WeightGrams { get; set; }
    public int? ShelfLifeHours { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ProductVariant> Variants { get; } = new();
}

public class ProductVariant : AuditableEntity
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    public int? WeightGrams { get; set; }
    public int? Servings { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class ProductAllergenInfo
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AllergenPresence Presence { get; set; }
}
public sealed class ProductDetails
{
    public required Product Product { get; init; }
    public required IReadOnlyList<ProductVariant> Variants { get; init; }
    public required IReadOnlyList<string> Ingredients { get; init; }
    public required IReadOnlyList<ProductAllergenInfo> Allergens { get; init; }
}
