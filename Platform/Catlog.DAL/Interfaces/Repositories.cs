using Catalog.Domain.Models;

namespace Catalog.Dal.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetAllAsync(bool activeOnly, CancellationToken ct = default);
    Task<Guid> AddAsync(Category category, CancellationToken ct = default);
    Task<bool> UpdateAsync(Category category, CancellationToken ct = default);

    ///  М'яке видалення (IsDeleted = 1, IsActive = 0). 
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default);
}

public interface IProductRepository : IGenericRepository<Product>
{
    ///  Товар з усіма не видаленими варіантами одним запитом (multi-mapping). Для адмін-частини. 
    Task<Product?> GetWithVariantsAsync(Guid productId, CancellationToken ct = default);

    ///  Сторінка товару для вітрини: активний товар, активні варіанти, склад, алергени (QueryMultiple). 
    Task<ProductDetails?> GetDetailsBySlugAsync(string slug, CancellationToken ct = default);

    ///  Список з фільтрами, сортуванням і пагінацією з vw_ProductListing. 
    Task<PagedResult<ProductListingItem>> GetListingAsync(
        ProductListingFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<ProductListingItem>> GetTopRatedAsync(int count, CancellationToken ct = default);

    ///  Замінює склад товару. Потребує активної транзакції Unit of Work. 
    Task ReplaceIngredientsAsync(Guid productId, IEnumerable<Guid> ingredientIds, CancellationToken ct = default);

    ///  Замінює алергени товару. Потребує активної транзакції Unit of Work. 
    Task ReplaceAllergensAsync(Guid productId,
        IEnumerable<(Guid AllergenId, Domain.Constants.AllergenPresence Presence)> allergens,
        CancellationToken ct = default);

    ///  
    /// Застосовує RatingSummaryChanged (usp_ProductRating_Apply): ідемпотентно за eventId,
    /// застарілі версії ігноруються. true — рейтинг оновлено.
    ///  
    Task<bool> ApplyRatingAsync(Guid eventId, Guid productId, decimal avgRating, int reviewCount,
        long ratingVersion, CancellationToken ct = default);
}

public interface IVariantRepository : IGenericRepository<ProductVariant>
{
    Task<IReadOnlyList<ProductVariant>> GetByProductAsync(Guid productId, bool includeInactive,
        CancellationToken ct = default);
}

public interface IStockRepository : IGenericRepository<StockItem>
{
    ///  
    /// usp_Stock_Reserve: атомарне резервування (ідемпотентне для пари OrderId + VariantId).
    /// Кидає InsufficientStockException / NotFoundException / ConflictException.
    ///  
    Task ReserveAsync(Guid orderId, Guid variantId, int quantity, DateTime? expiresAtUtc,
        CancellationToken ct = default);

    ///  usp_Stock_Commit: резервації замовлення → Committed. NotFoundException, якщо активних немає. 
    Task CommitAsync(Guid orderId, CancellationToken ct = default);

    ///  usp_Stock_Release: повертає залишок за активними резерваціями замовлення. Повертає кількість звільнених. 
    Task<int> ReleaseAsync(Guid orderId, CancellationToken ct = default);

    ///  usp_Stock_ReleaseExpired: звільняє прострочені резервації пачкою. Повертає кількість звільнених. 
    Task<int> ReleaseExpiredAsync(int batchSize, CancellationToken ct = default);

    ///  usp_Stock_ResetDaily: QuantityAvailable = DailyLimit мінус активні резервації. Повертає кількість змінених рядків. 
    Task<int> ResetDailyAsync(CancellationToken ct = default);

    Task<IReadOnlyList<StockReservation>> GetReservationsAsync(Guid orderId, CancellationToken ct = default);
}

public interface IOutboxRepository
{
    ///  Пише подію в Outbox. Потребує активної транзакції — подія має бути атомарна зі зміною даних. 
    Task<Guid> AddAsync(string eventType, Guid aggregateId, string payloadJson, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(IReadOnlyCollection<long> outboxIds, CancellationToken ct = default);
}

public interface IReferenceRepository
{
    Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Allergen>> GetAllergensAsync(CancellationToken ct = default);
}
