using System.Diagnostics;
using System.Globalization;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Domain.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Platform.Contracts.Catalog;
using DomainSort = Catalog.Domain.Constants.ProductSort;

namespace Catalog.Api.GrpcServices;

/// <summary>gRPC-адаптер над BLL (як контролер): валідує вхід, викликає ті самі сервіси з кешем, що й REST,
/// і перетворює DTO в повідомлення. Доменні винятки в RpcException переводить <see cref="DomainExceptionInterceptor"/>.</summary>
public sealed class CatalogGrpcService(
    IProductService products,
    ICategoryService categories,
    IStockService stock,
    ILogger<CatalogGrpcService> logger) : CatalogGrpc.CatalogGrpcBase
{
    private const int MaxIds = 100;

    // ====================== читання ======================

    public override async Task<ProductInfo> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        var id = ParseGuid(request.ProductId, nameof(request.ProductId));
        Activity.Current?.SetTag("catalog.product_id", id);   // бізнес-контекст на span
        return ToInfo(await products.GetByIdAsync(id, context.CancellationToken));
    }

    public override async Task<GetProductsByIdsReply> GetProductsByIds(
        GetProductsByIdsRequest request, ServerCallContext context)
    {
        if (request.ProductIds.Count == 0)
            throw Invalid("product_ids не може бути порожнім.");
        if (request.ProductIds.Count > MaxIds)
            throw Invalid($"Забагато ідентифікаторів (максимум {MaxIds}).");

        var ids = request.ProductIds.Select(i => ParseGuid(i, "product_ids")).Distinct().ToList();
        var reply = new GetProductsByIdsReply();

        // Послідовно: UnitOfWork має одне з'єднання на запит.
        foreach (var id in ids)
        {
            try
            {
                reply.Products.Add(ToInfo(await products.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (NotFoundException)
            {
                reply.MissingIds.Add(id.ToString());
            }
        }

        logger.LogInformation("gRPC GetProductsByIds: запитано {Requested}, знайдено {Found}",
            ids.Count, reply.Products.Count);
        return reply;
    }

    public override async Task<ProductDetailsInfo> GetProductBySlug(
        GetProductBySlugRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            throw Invalid("slug обов'язковий.");

        Activity.Current?.SetTag("catalog.slug", request.Slug);
        return ToInfo(await products.GetDetailsBySlugAsync(request.Slug, context.CancellationToken));
    }

    public override async Task<ListProductsReply> ListProducts(ListProductsRequest request, ServerCallContext context)
    {
        var query = new ListingQuery
        {
            CategorySlug = string.IsNullOrWhiteSpace(request.CategorySlug) ? null : request.CategorySlug,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search,
            InStockOnly = request.InStockOnly,
            MinPrice = request.HasMinPrice ? ParseDecimal(request.MinPrice, "min_price") : null,
            MaxPrice = request.HasMaxPrice ? ParseDecimal(request.MaxPrice, "max_price") : null,
            Sort = request.Sort switch
            {
                ProductSortOrder.PriceAsc => DomainSort.PriceAsc,
                ProductSortOrder.PriceDesc => DomainSort.PriceDesc,
                ProductSortOrder.Rating => DomainSort.Rating,
                _ => DomainSort.Newest
            },
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 20 : request.PageSize
        };

        var result = await products.GetListingAsync(query, context.CancellationToken);

        var reply = new ListProductsReply { Total = result.Total, Page = result.Page, PageSize = result.PageSize };
        reply.Items.AddRange(result.Items.Select(ToInfo));
        return reply;
    }

    public override async Task<GetTopRatedReply> GetTopRated(GetTopRatedRequest request, ServerCallContext context)
    {
        if (request.Count < 0)
            throw Invalid("count не може бути від'ємним.");

        var items = await products.GetTopRatedAsync(request.Count == 0 ? 10 : request.Count, context.CancellationToken);

        var reply = new GetTopRatedReply();
        reply.Items.AddRange(items.Select(ToInfo));
        return reply;
    }

    public override async Task<ListCategoriesReply> ListCategories(
        ListCategoriesRequest request, ServerCallContext context)
    {
        var items = await categories.ListAsync(request.ActiveOnly, context.CancellationToken);

        var reply = new ListCategoriesReply();
        reply.Categories.AddRange(items.Select(c => new CategoryInfo
        {
            CategoryId = c.Id.ToString(),
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description ?? "",
            SortOrder = c.SortOrder,
            IsActive = c.IsActive
        }));
        return reply;
    }

    // ====================== залишки й резерви ======================

    public override async Task<StockInfo> GetStock(GetStockRequest request, ServerCallContext context)
    {
        var id = ParseGuid(request.VariantId, nameof(request.VariantId));
        var s = await stock.GetAsync(id, context.CancellationToken);
        return new StockInfo
        {
            VariantId = s.VariantId.ToString(),
            QuantityAvailable = s.QuantityAvailable,
            DailyLimit = s.DailyLimit
        };
    }

    public override async Task<ReservationInfo> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        var orderId = ParseGuid(request.OrderId, nameof(request.OrderId));
        Activity.Current?.SetTag("order.id", orderId);
        Activity.Current?.SetTag("catalog.reserve.lines", request.Lines.Count);
        if (request.Lines.Count == 0)
            throw Invalid("Потрібна принаймні одна позиція.");

        var lines = request.Lines
            .Select(l => new ReserveLine(ParseGuid(l.VariantId, "lines.variant_id"), l.Quantity))
            .ToList();

        // Решту правил (кількість > 0, ліміт позицій, дублікати, TTL) перевіряє BLL — ті самі, що й для REST.
        var reservation = await stock.ReserveAsync(
            new ReserveRequest(orderId, lines, request.HasTtlMinutes ? request.TtlMinutes : null),
            context.CancellationToken);

        var reply = new ReservationInfo
        {
            OrderId = reservation.OrderId.ToString(),
            ExpiresAt = Timestamp.FromDateTime(DateTime.SpecifyKind(reservation.ExpiresAtUtc, DateTimeKind.Utc))
        };
        reply.Lines.AddRange(reservation.Lines.Select(l =>
            new ReserveStockLine { VariantId = l.VariantId.ToString(), Quantity = l.Quantity }));
        return reply;
    }

    public override async Task<Empty> CommitReservation(CommitReservationRequest request, ServerCallContext context)
    {
        await stock.CommitAsync(ParseGuid(request.OrderId, nameof(request.OrderId)), context.CancellationToken);
        return new Empty();
    }

    public override async Task<ReleaseReservationReply> ReleaseReservation(
        ReleaseReservationRequest request, ServerCallContext context)
    {
        var released = await stock.ReleaseAsync(
            ParseGuid(request.OrderId, nameof(request.OrderId)), context.CancellationToken);
        return new ReleaseReservationReply { Released = released };
    }

    // ====================== перетворення ======================

    private static ProductInfo ToInfo(ProductDto p)
    {
        var info = new ProductInfo
        {
            ProductId = p.ProductId.ToString(),
            CategoryId = p.CategoryId.ToString(),
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description ?? "",
            ImageUrl = p.ImageUrl ?? "",
            BasePrice = Money(p.BasePrice),
            IsActive = p.IsActive
        };
        if (p.WeightGrams is { } w) info.WeightGrams = w;
        if (p.ShelfLifeHours is { } h) info.ShelfLifeHours = h;
        info.Variants.AddRange(p.Variants.Select(v =>
        {
            var vi = new VariantInfo
            {
                VariantId = v.VariantId.ToString(),
                Name = v.Name,
                Sku = v.Sku,
                Price = Money(v.Price),
                IsActive = v.IsActive
            };
            if (v.WeightGrams is { } vw) vi.WeightGrams = vw;
            if (v.Servings is { } s) vi.Servings = s;
            return vi;
        }));
        return info;
    }

    private static ProductDetailsInfo ToInfo(ProductDetailsDto p)
    {
        var info = new ProductDetailsInfo
        {
            ProductId = p.ProductId.ToString(),
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description ?? "",
            ImageUrl = p.ImageUrl ?? "",
            BasePrice = Money(p.BasePrice)
        };
        if (p.WeightGrams is { } w) info.WeightGrams = w;
        if (p.ShelfLifeHours is { } h) info.ShelfLifeHours = h;

        // На вітрині показують лише активні варіанти.
        info.Variants.AddRange(p.Variants.Select(v =>
        {
            var vi = new VariantInfo
            {
                VariantId = v.VariantId.ToString(),
                Name = v.Name,
                Sku = v.Sku,
                Price = Money(v.Price),
                IsActive = true
            };
            if (v.WeightGrams is { } vw) vi.WeightGrams = vw;
            if (v.Servings is { } s) vi.Servings = s;
            return vi;
        }));
        info.Ingredients.AddRange(p.Ingredients);
        info.Allergens.AddRange(p.Allergens.Select(a =>
            new AllergenInfo { Code = a.Code, Name = a.Name, Presence = a.Presence }));
        return info;
    }

    private static ProductListItemInfo ToInfo(ProductListItemDto p)
    {
        var info = new ProductListItemInfo
        {
            ProductId = p.ProductId.ToString(),
            Name = p.Name,
            Slug = p.Slug,
            ImageUrl = p.ImageUrl ?? "",
            CategoryId = p.CategoryId.ToString(),
            CategoryName = p.CategoryName,
            CategorySlug = p.CategorySlug,
            BasePrice = Money(p.BasePrice),
            VariantCount = p.VariantCount,
            TotalAvailable = p.TotalAvailable,
            InStock = p.InStock,
            AvgRating = Money(p.AvgRating),
            ReviewCount = p.ReviewCount,
            Allergens = p.Allergens ?? ""
        };
        if (p.MinPrice is { } min) info.MinPrice = Money(min);
        if (p.MaxPrice is { } max) info.MaxPrice = Money(max);
        return info;
    }

    // ====================== допоміжне ======================

    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value, string field) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw Invalid($"{field}: '{value}' не є числом (очікується формат 125.50).");

    private static Guid ParseGuid(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw Invalid($"{field}: '{value}' не є коректним GUID.");

    private static RpcException Invalid(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
