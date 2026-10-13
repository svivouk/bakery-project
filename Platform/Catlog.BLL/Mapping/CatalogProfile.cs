using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Domain.Constants;
using Catalog.Domain.Models;

namespace Catalog.Bll.Mapping;

/// <summary>
/// Єдиний профіль AutoMapper для контексту «Каталог»: модель домену → DTO API-контракту.
/// DTO — позиційні record-и, тому нетривіальні поля задаються через ForCtorParam.
/// Реєструється в DI одним викликом AddAutoMapper (див. Program.cs).
/// </summary>
public sealed class CatalogProfile : Profile
{
    public CatalogProfile()
    {
        // ---------- довідники ----------
        CreateMap<Category, CategoryDto>()
            .ForCtorParam(nameof(CategoryDto.Id), o => o.MapFrom(s => s.CategoryId));

        CreateMap<Ingredient, IngredientDto>()
            .ForCtorParam(nameof(IngredientDto.Id), o => o.MapFrom(s => s.IngredientId));

        CreateMap<Allergen, AllergenRefDto>()
            .ForCtorParam(nameof(AllergenRefDto.Id), o => o.MapFrom(s => s.AllergenId));

        // ---------- товари та варіанти ----------
        CreateMap<ProductVariant, VariantDto>();
        CreateMap<ProductVariant, VariantInfoDto>();

        CreateMap<Product, ProductDto>()
            .ForCtorParam(nameof(ProductDto.IsActive), o => o.MapFrom(s => s.Status == ProductStatus.Active));

        CreateMap<ProductListingItem, ProductListItemDto>();

        CreateMap<ProductAllergenInfo, AllergenDto>()
            .ForCtorParam(nameof(AllergenDto.Presence), o => o.MapFrom(s => s.Presence.ToString()));

        // Сторінка товару: частина полів лежить у вкладеному Product.
        CreateMap<ProductDetails, ProductDetailsDto>()
            .ForCtorParam(nameof(ProductDetailsDto.ProductId), o => o.MapFrom(s => s.Product.ProductId))
            .ForCtorParam(nameof(ProductDetailsDto.Name), o => o.MapFrom(s => s.Product.Name))
            .ForCtorParam(nameof(ProductDetailsDto.Slug), o => o.MapFrom(s => s.Product.Slug))
            .ForCtorParam(nameof(ProductDetailsDto.Description), o => o.MapFrom(s => s.Product.Description))
            .ForCtorParam(nameof(ProductDetailsDto.ImageUrl), o => o.MapFrom(s => s.Product.ImageUrl))
            .ForCtorParam(nameof(ProductDetailsDto.BasePrice), o => o.MapFrom(s => s.Product.BasePrice))
            .ForCtorParam(nameof(ProductDetailsDto.WeightGrams), o => o.MapFrom(s => s.Product.WeightGrams))
            .ForCtorParam(nameof(ProductDetailsDto.ShelfLifeHours), o => o.MapFrom(s => s.Product.ShelfLifeHours));

        // ---------- склад ----------
        CreateMap<StockItem, StockDto>();
    }
}
