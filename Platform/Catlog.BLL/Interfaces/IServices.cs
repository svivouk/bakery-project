using Catalog.Bll.Dto;

namespace Catalog.Bll.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<CategoryDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProductService
{
    // вітрина
    Task<PagedResultDto<ProductListItemDto>> GetListingAsync(ListingQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItemDto>> GetTopRatedAsync(int count, CancellationToken ct = default);
    Task<ProductDetailsDto> GetDetailsBySlugAsync(string slug, CancellationToken ct = default);

    // адмін-частина
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<VariantDto> AddVariantAsync(Guid productId, CreateVariantRequest request, CancellationToken ct = default);
    Task<VariantDto> UpdateVariantAsync(Guid productId, Guid variantId, UpdateVariantRequest request,
        CancellationToken ct = default);
    Task DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken ct = default);
    Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AllergenRefDto>> GetAllergensAsync(CancellationToken ct = default);
    Task<bool> ApplyRatingSummaryAsync(RatingSummaryChangedEvent e, CancellationToken ct = default);
}

public interface IStockService
{
    Task<StockDto> GetAsync(Guid variantId, CancellationToken ct = default);
    Task<StockDto> AdjustAsync(Guid variantId, AdjustStockRequest request, CancellationToken ct = default);

    Task<ReservationDto> ReserveAsync(ReserveRequest request, CancellationToken ct = default);
    Task CommitAsync(Guid orderId, CancellationToken ct = default);
    Task<int> ReleaseAsync(Guid orderId, CancellationToken ct = default);

    // обслуговування
    Task<int> ReleaseExpiredAsync(CancellationToken ct = default);
    Task<int> ResetDailyAsync(CancellationToken ct = default);
}
