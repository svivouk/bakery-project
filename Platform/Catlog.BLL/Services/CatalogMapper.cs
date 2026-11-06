using Catalog.Bll.Dto;
using Catalog.Domain.Constants;
using Catalog.Domain.Models;

namespace Catalog.Bll.Services;

///  Ручний мапінг модель → DTO (без AutoMapper: кілька сутностей, прозорий код). 
internal static class CatalogMapper
{
    public static CategoryDto ToDto(this Category c) =>
        new(c.CategoryId, c.Name, c.Slug, c.Description, c.SortOrder, c.IsActive);

    public static VariantDto ToDto(this ProductVariant v) =>
        new(v.VariantId, v.ProductId, v.Name, v.Sku, v.Price, v.WeightGrams, v.Servings,
            v.SortOrder, v.IsActive, v.RowVersion);

    public static VariantInfoDto ToInfoDto(this ProductVariant v) =>
        new(v.VariantId, v.Name, v.Sku, v.Price, v.WeightGrams, v.Servings);

    public static ProductDto ToDto(this Product p) =>
        new(p.ProductId, p.CategoryId, p.Name, p.Slug, p.Description, p.ImageUrl, p.BasePrice,
            p.WeightGrams, p.ShelfLifeHours, p.Status == ProductStatus.Active, p.RowVersion,
            p.Variants.Select(v => v.ToDto()).ToList());

    public static ProductListItemDto ToDto(this ProductListingItem x) =>
        new(x.ProductId, x.Name, x.Slug, x.ImageUrl, x.CategoryId, x.CategoryName, x.CategorySlug,
            x.BasePrice, x.MinPrice, x.MaxPrice, x.VariantCount, x.TotalAvailable, x.InStock,
            x.AvgRating, x.ReviewCount, x.Allergens);

    public static ProductDetailsDto ToDto(this ProductDetails d) =>
        new(d.Product.ProductId, d.Product.Name, d.Product.Slug, d.Product.Description, d.Product.ImageUrl,
            d.Product.BasePrice, d.Product.WeightGrams, d.Product.ShelfLifeHours,
            d.Variants.Select(v => v.ToInfoDto()).ToList(),
            d.Ingredients,
            d.Allergens.Select(a => new AllergenDto(a.Code, a.Name, a.Presence.ToString())).ToList());

    public static StockDto ToDto(this StockItem s) => new(s.VariantId, s.QuantityAvailable, s.DailyLimit);
}
