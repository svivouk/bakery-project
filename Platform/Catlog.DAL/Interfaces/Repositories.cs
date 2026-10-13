using Catalog.Domain.Models;

namespace Catalog.Dal.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetAllAsync(bool activeOnly, CancellationToken ct = default);
    Task<Guid> AddAsync(Category category, CancellationToken ct = default);
    Task<bool> UpdateAsync(Category category, CancellationToken ct = default);

    /// <summary>М'яке видалення (IsDeleted = 1, IsActive = 0).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default);
}

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Товар з усіма не видаленими варіантами одним запитом (multi-mapping). Для адмін-частини.</summary>
    Task<Product?> GetWithVariantsAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Сторінка товару для вітрини: активний товар, активні варіанти, склад, алергени (QueryMultiple).</summary>
    Task<ProductDetails?> GetDetailsBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Список з фільтрами, сортуванням і пагінацією з vw_ProductListing.</summary>
    Task<PagedResult<ProductListingItem>> GetListingAsync(
        ProductListingFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<ProductListingItem>> GetTopRatedAsync(int count, CancellationToken ct = default);

    /// <summary>Замінює склад товару. Потребує активної транзакції Unit of Work.</summary>
    Task ReplaceIngredientsAsync(Guid productId, IEnumerable<Guid> ingredientIds, CancellationToken ct = default);

    /// <summary>Замінює алергени товару. Потребує активної транзакції Unit of Work.</summary>
    Task ReplaceAllergensAsync(Guid productId,
        IEnumerable<(Guid AllergenId, Domain.Constants.AllergenPresence Presence)> allergens,
        CancellationToken ct = default);

    /// <summary>
    /// Застосовує RatingSummaryChanged (usp_ProductRating_Apply): ідемпотентно за eventId,
    /// застарілі версії ігноруються. true — рейтинг оновлено.
    /// </summary>
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
    /// <summary>
    /// usp_Stock_Reserve: атомарне резервування (ідемпотентне для пари OrderId + VariantId).
    /// Кидає InsufficientStockException / NotFoundException / ConflictException.
    /// </summary>
    Task ReserveAsync(Guid orderId, Guid variantId, int quantity, DateTime? expiresAtUtc,
        CancellationToken ct = default);

    /// <summary>usp_Stock_Commit: резервації замовлення → Committed. NotFoundException, якщо активних немає.</summary>
    Task CommitAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>usp_Stock_Release: повертає залишок за активними резерваціями замовлення. Повертає кількість звільнених.</summary>
    Task<int> ReleaseAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>usp_Stock_ReleaseExpired: звільняє прострочені резервації пачкою. Повертає кількість звільнених.</summary>
    Task<int> ReleaseExpiredAsync(int batchSize, CancellationToken ct = default);

    /// <summary>usp_Stock_ResetDaily: QuantityAvailable = DailyLimit мінус активні резервації. Повертає кількість змінених рядків.</summary>
    Task<int> ResetDailyAsync(CancellationToken ct = default);

    Task<IReadOnlyList<StockReservation>> GetReservationsAsync(Guid orderId, CancellationToken ct = default);
}

public interface IOutboxRepository
{
    /// <summary>Пише подію в Outbox. Потребує активної транзакції — подія має бути атомарна зі зміною даних.</summary>
    Task<Guid> AddAsync(string eventType, Guid aggregateId, string payloadJson, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(IReadOnlyCollection<long> outboxIds, CancellationToken ct = default);
}

public interface IReferenceRepository
{
    Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Allergen>> GetAllergensAsync(CancellationToken ct = default);
}
