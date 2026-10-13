using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Constants;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;

namespace Catalog.Bll.Services;

public sealed class ProductService : IProductService
{
    private const int MaxPageSize = 100;

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    // ====================== вітрина ======================

    public async Task<PagedResultDto<ProductListItemDto>> GetListingAsync(ListingQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        if (query.MinPrice is < 0 || query.MaxPrice is < 0)
            throw new ValidationException("Ціна не може бути від'ємною.");
        if (query.MinPrice > query.MaxPrice)
            throw new ValidationException("minPrice не може бути більшою за maxPrice.");

        var filter = new ProductListingFilter
        {
            CategorySlug = string.IsNullOrWhiteSpace(query.CategorySlug) ? null : query.CategorySlug.Trim().ToLowerInvariant(),
            Search = query.Search,
            InStockOnly = query.InStockOnly,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            Sort = query.Sort
        };

        var result = await _uow.Products.GetListingAsync(filter, page, pageSize, ct);
        return new PagedResultDto<ProductListItemDto>(
            _mapper.Map<List<ProductListItemDto>>(result.Items), result.Total, page, pageSize);
    }

    public async Task<IReadOnlyList<ProductListItemDto>> GetTopRatedAsync(int count, CancellationToken ct = default)
    {
        var items = await _uow.Products.GetTopRatedAsync(Math.Clamp(count, 1, MaxPageSize), ct);
        return _mapper.Map<List<ProductListItemDto>>(items);
    }

    public async Task<ProductDetailsDto> GetDetailsBySlugAsync(string slug, CancellationToken ct = default)
    {
        var details = await _uow.Products.GetDetailsBySlugAsync(slug.Trim().ToLowerInvariant(), ct)
                      ?? throw new NotFoundException("Product", slug);
        return _mapper.Map<ProductDetailsDto>(details);
    }

    // ====================== адмін-частина ======================

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetWithVariantsAsync(id, ct)
                      ?? throw new NotFoundException("Product", id);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = Check.Text(request.Name, "name", 200),
            Slug = Check.Slug(request.Slug, "slug", 220),
            Description = Check.OptionalText(request.Description, "description", 2000),
            ImageUrl = Check.OptionalText(request.ImageUrl, "imageUrl", 500),
            BasePrice = Check.Price(request.BasePrice, "basePrice"),
            WeightGrams = Check.PositiveOrNull(request.WeightGrams, "weightGrams"),
            ShelfLifeHours = Check.PositiveOrNull(request.ShelfLifeHours, "shelfLifeHours"),
            Status = ProductStatus.Active
        };

        if (await _uow.Categories.GetByIdAsync(request.CategoryId, ct) is null)
            throw new NotFoundException("Category", request.CategoryId);

        var productId = await _uow.InTransactionAsync(async () =>
        {
            var id = await _uow.Products.AddAsync(product, ct);

            if (request.IngredientIds is { Count: > 0 })
                await _uow.Products.ReplaceIngredientsAsync(id, request.IngredientIds, ct);

            if (request.Allergens is { Count: > 0 })
                await _uow.Products.ReplaceAllergensAsync(
                    id, request.Allergens.Select(a => (a.AllergenId, a.Presence)), ct);

            foreach (var variant in request.Variants ?? Array.Empty<CreateVariantRequest>())
                await AddVariantCoreAsync(id, variant, ct);

            await EventPublisher.PublishAsync(_uow, EventTypes.ProductCreated, id, new
            {
                productId = id,
                categoryId = product.CategoryId,
                name = product.Name,
                slug = product.Slug,
                basePrice = product.BasePrice
            }, ct);

            return id;
        }, ct);

        return await GetByIdAsync(productId, ct);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var updated = new Product
        {
            ProductId = id,
            CategoryId = request.CategoryId,
            Name = Check.Text(request.Name, "name", 200),
            Slug = Check.Slug(request.Slug, "slug", 220),
            Description = Check.OptionalText(request.Description, "description", 2000),
            ImageUrl = Check.OptionalText(request.ImageUrl, "imageUrl", 500),
            BasePrice = Check.Price(request.BasePrice, "basePrice"),
            WeightGrams = Check.PositiveOrNull(request.WeightGrams, "weightGrams"),
            ShelfLifeHours = Check.PositiveOrNull(request.ShelfLifeHours, "shelfLifeHours"),
            Status = request.IsActive ? ProductStatus.Active : ProductStatus.Hidden,
            RowVersion = Check.RowVersion(request.RowVersion)
        };

        await _uow.InTransactionAsync(async () =>
        {
            var existing = await _uow.Products.GetByIdAsync(id, ct)
                           ?? throw new NotFoundException("Product", id);

            if (existing.CategoryId != updated.CategoryId
                && await _uow.Categories.GetByIdAsync(updated.CategoryId, ct) is null)
                throw new NotFoundException("Category", updated.CategoryId);

            // Оновлення з умовою на RowVersion: false означає, що товар змінили після того, як клієнт його прочитав.
            if (!await _uow.Products.UpdateAsync(updated, ct))
                throw new ConcurrencyException("Product", id);

            if (request.IngredientIds is not null)
                await _uow.Products.ReplaceIngredientsAsync(id, request.IngredientIds, ct);

            if (request.Allergens is not null)
                await _uow.Products.ReplaceAllergensAsync(
                    id, request.Allergens.Select(a => (a.AllergenId, a.Presence)), ct);

            if (existing.Name != updated.Name || existing.Slug != updated.Slug)
                await EventPublisher.PublishAsync(_uow, EventTypes.ProductRenamed, id, new
                {
                    productId = id,
                    oldName = existing.Name,
                    newName = updated.Name,
                    slug = updated.Slug
                }, ct);

            if (existing.BasePrice != updated.BasePrice)
                await EventPublisher.PublishAsync(_uow, EventTypes.PriceChanged, id, new
                {
                    productId = id,
                    variantId = (Guid?)null,
                    oldPrice = existing.BasePrice,
                    newPrice = updated.BasePrice
                }, ct);

            if (existing.Status == ProductStatus.Active && updated.Status == ProductStatus.Hidden)
                await EventPublisher.PublishAsync(_uow, EventTypes.ProductDeactivated, id,
                    new { productId = id }, ct);
        }, ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _uow.InTransactionAsync(async () =>
        {
            if (!await _uow.Products.DeleteAsync(id, ct))
                throw new NotFoundException("Product", id);

            await EventPublisher.PublishAsync(_uow, EventTypes.ProductDeactivated, id,
                new { productId = id }, ct);
        }, ct);
    }

    // ====================== варіанти ======================

    public async Task<VariantDto> AddVariantAsync(Guid productId, CreateVariantRequest request,
        CancellationToken ct = default)
    {
        var variantId = await _uow.InTransactionAsync(async () =>
        {
            if (await _uow.Products.GetByIdAsync(productId, ct) is null)
                throw new NotFoundException("Product", productId);

            return await AddVariantCoreAsync(productId, request, ct);
        }, ct);

        var variant = await _uow.Variants.GetByIdAsync(variantId, ct)
                      ?? throw new NotFoundException("Variant", variantId);
        return _mapper.Map<VariantDto>(variant);
    }

    public async Task<VariantDto> UpdateVariantAsync(Guid productId, Guid variantId, UpdateVariantRequest request,
        CancellationToken ct = default)
    {
        var updated = new ProductVariant
        {
            VariantId = variantId,
            ProductId = productId,
            Name = Check.Text(request.Name, "name", 100),
            Sku = Check.Text(request.Sku, "sku", 40),
            Price = Check.Price(request.Price, "price"),
            WeightGrams = Check.PositiveOrNull(request.WeightGrams, "weightGrams"),
            Servings = Check.PositiveOrNull(request.Servings, "servings"),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            RowVersion = Check.RowVersion(request.RowVersion)
        };

        await _uow.InTransactionAsync(async () =>
        {
            var existing = await _uow.Variants.GetByIdAsync(variantId, ct);
            if (existing is null || existing.ProductId != productId)
                throw new NotFoundException("Variant", variantId);

            if (!await _uow.Variants.UpdateAsync(updated, ct))
                throw new ConcurrencyException("Variant", variantId);

            if (existing.Price != updated.Price)
                await EventPublisher.PublishAsync(_uow, EventTypes.PriceChanged, productId, new
                {
                    productId,
                    variantId,
                    oldPrice = existing.Price,
                    newPrice = updated.Price
                }, ct);
        }, ct);

        var variant = await _uow.Variants.GetByIdAsync(variantId, ct)
                      ?? throw new NotFoundException("Variant", variantId);
        return _mapper.Map<VariantDto>(variant);
    }

    public async Task DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken ct = default)
    {
        var existing = await _uow.Variants.GetByIdAsync(variantId, ct);
        if (existing is null || existing.ProductId != productId)
            throw new NotFoundException("Variant", variantId);

        // М'яке видалення: IsDeleted = 1, IsActive = 0 (варіант не видаляють фізично).
        if (!await _uow.Variants.DeleteAsync(variantId, ct))
            throw new NotFoundException("Variant", variantId);
    }

    /// <summary>Створює варіант і його рядок залишків. Викликається всередині транзакції.</summary>
    private async Task<Guid> AddVariantCoreAsync(Guid productId, CreateVariantRequest request, CancellationToken ct)
    {
        var variant = new ProductVariant
        {
            ProductId = productId,
            Name = Check.Text(request.Name, "variant.name", 100),
            Sku = Check.Text(request.Sku, "variant.sku", 40),
            Price = Check.Price(request.Price, "variant.price"),
            WeightGrams = Check.PositiveOrNull(request.WeightGrams, "variant.weightGrams"),
            Servings = Check.PositiveOrNull(request.Servings, "variant.servings"),
            SortOrder = request.SortOrder,
            IsActive = true
        };
        var quantity = Check.NonNegative(request.InitialQuantity, "variant.initialQuantity");
        var dailyLimit = Check.NonNegative(request.DailyLimit, "variant.dailyLimit");

        var variantId = await _uow.Variants.AddAsync(variant, ct);

        await _uow.Stock.AddAsync(new StockItem
        {
            VariantId = variantId,
            QuantityAvailable = quantity,
            DailyLimit = dailyLimit
        }, ct);

        await EventPublisher.PublishAsync(_uow, EventTypes.PriceChanged, productId, new
        {
            productId,
            variantId,
            oldPrice = (decimal?)null,
            newPrice = variant.Price
        }, ct);

        await EventPublisher.PublishAsync(_uow, EventTypes.StockChanged, productId, new
        {
            productId,
            variantId,
            quantityAvailable = quantity,
            reason = "created"
        }, ct);

        return variantId;
    }

    // ====================== довідники та Inbox ======================

    public async Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(CancellationToken ct = default)
    {
        var rows = await _uow.Reference.GetIngredientsAsync(ct);
        return _mapper.Map<List<IngredientDto>>(rows);
    }

    public async Task<IReadOnlyList<AllergenRefDto>> GetAllergensAsync(CancellationToken ct = default)
    {
        var rows = await _uow.Reference.GetAllergensAsync(ct);
        return _mapper.Map<List<AllergenRefDto>>(rows);
    }

    public Task<bool> ApplyRatingSummaryAsync(RatingSummaryChangedEvent e, CancellationToken ct = default)
    {
        if (e.EventId == Guid.Empty || e.ProductId == Guid.Empty)
            throw new ValidationException("eventId і productId обов'язкові.");
        if (e.AvgRating is < 0 or > 5)
            throw new ValidationException("avgRating має бути в діапазоні 0–5.");
        if (e.ReviewCount < 0)
            throw new ValidationException("reviewCount не може бути від'ємним.");

        // Ідемпотентність і відсікання застарілих версій — всередині usp_ProductRating_Apply (одна транзакція).
        return _uow.Products.ApplyRatingAsync(
            e.EventId, e.ProductId, decimal.Round(e.AvgRating, 2), e.ReviewCount, e.Version, ct);
    }
}
