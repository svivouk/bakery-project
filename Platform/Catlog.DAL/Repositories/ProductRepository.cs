using System.Data;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Constants;
using Catalog.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Catalog.Dal.Repositories;

/// <summary>Dapper: CRUD з базового класу + multi-mapping, QueryMultiple, читання з view і виклик процедури.</summary>
public sealed class ProductRepository : BaseDapperRepository<Product>, IProductRepository
{
    public ProductRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Products", "ProductId", softDelete: true) { }

    protected override string InsertSql => @"
        INSERT INTO dbo.Products (CategoryId, Name, Slug, Description, ImageUrl, BasePrice,
                                  WeightGrams, ShelfLifeHours, Status)
        OUTPUT INSERTED.ProductId
        VALUES (@CategoryId, @Name, @Slug, @Description, @ImageUrl, @BasePrice,
                @WeightGrams, @ShelfLifeHours, @Status)";

    // Оптимістична конкурентність: оновлюємо лише якщо RowVersion не змінився.
    protected override string UpdateSql => @"
        UPDATE dbo.Products
        SET CategoryId = @CategoryId, Name = @Name, Slug = @Slug, Description = @Description,
            ImageUrl = @ImageUrl, BasePrice = @BasePrice, WeightGrams = @WeightGrams,
            ShelfLifeHours = @ShelfLifeHours, Status = @Status,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
        WHERE ProductId = @ProductId AND RowVersion = @RowVersion AND IsDeleted = 0";

    // CK_Products_Deleted: IsDeleted = 1 вимагає Status = 0. Варіанти видаляємо разом із товаром.
    protected override string DeleteSql => @"
        UPDATE dbo.Products
        SET IsDeleted = 1, Status = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
        WHERE ProductId = @Id AND IsDeleted = 0;
        IF @@ROWCOUNT > 0
            UPDATE dbo.ProductVariants
            SET IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = SUSER_SNAME()
            WHERE ProductId = @Id AND IsDeleted = 0;";

    // ---------- multi-mapping: товар + варіанти одним запитом ----------

    public async Task<Product?> GetWithVariantsAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.*, v.*
            FROM dbo.Products p
            LEFT JOIN dbo.ProductVariants v ON v.ProductId = p.ProductId AND v.IsDeleted = 0
            WHERE p.ProductId = @ProductId AND p.IsDeleted = 0
            ORDER BY v.SortOrder, v.Name";

        await EnsureOpenAsync(ct);

        Product? result = null;
        await Connection.QueryAsync<Product, ProductVariant?, Product>(
            new CommandDefinition(sql, new { ProductId = productId }, Transaction, cancellationToken: ct),
            (product, variant) =>
            {
                result ??= product;                       // усі рядки JOIN належать одному товару
                if (variant is not null) result.Variants.Add(variant);
                return result;
            },
            splitOn: "VariantId");                        // з цього стовпця починається ProductVariant
        return result;
    }

    // ---------- QueryMultiple: сторінка товару для вітрини ----------

    public async Task<ProductDetails?> GetDetailsBySlugAsync(string slug, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT * FROM dbo.Products
            WHERE Slug = @Slug AND Status = 1 AND IsDeleted = 0;

            SELECT v.*
            FROM dbo.ProductVariants v
            JOIN dbo.Products p ON p.ProductId = v.ProductId
            WHERE p.Slug = @Slug AND p.Status = 1 AND p.IsDeleted = 0
              AND v.IsActive = 1 AND v.IsDeleted = 0
            ORDER BY v.SortOrder, v.Name;

            SELECT i.Name
            FROM dbo.Products p
            JOIN dbo.ProductIngredients pri ON pri.ProductId = p.ProductId
            JOIN dbo.Ingredients i ON i.IngredientId = pri.IngredientId AND i.IsDeleted = 0
            WHERE p.Slug = @Slug AND p.Status = 1 AND p.IsDeleted = 0
            ORDER BY i.Name;

            SELECT a.Code, a.Name, pa.Presence
            FROM dbo.Products p
            JOIN dbo.ProductAllergens pa ON pa.ProductId = p.ProductId
            JOIN dbo.Allergens a ON a.AllergenId = pa.AllergenId
            WHERE p.Slug = @Slug AND p.Status = 1 AND p.IsDeleted = 0
            ORDER BY pa.Presence, a.Name;";

        await EnsureOpenAsync(ct);

        using var grid = await Connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Slug = Ansi(slug, 220) }, Transaction, cancellationToken: ct));

        var product = await grid.ReadSingleOrDefaultAsync<Product>();
        if (product is null) return null;

        var variants = (await grid.ReadAsync<ProductVariant>()).AsList();
        var ingredients = (await grid.ReadAsync<string>()).AsList();
        var allergens = (await grid.ReadAsync<ProductAllergenInfo>()).AsList();

        return new ProductDetails
        {
            Product = product,
            Variants = variants,
            Ingredients = ingredients,
            Allergens = allergens
        };
    }

    // ---------- читання з view vw_ProductListing ----------

    private const string ListingWhere = @"
        WHERE (@CategorySlug IS NULL OR CategorySlug = @CategorySlug)
          AND (@SearchPattern IS NULL OR Name LIKE @SearchPattern ESCAPE '\')
          AND (@InStockOnly = 0 OR InStock = 1)
          AND (@MinPrice IS NULL OR COALESCE(MinPrice, BasePrice) >= @MinPrice)
          AND (@MaxPrice IS NULL OR COALESCE(MinPrice, BasePrice) <= @MaxPrice)";

    // Білий список: у ORDER BY ніколи не підставляється рядок від користувача.
    private static string OrderBy(ProductSort sort) => sort switch
    {
        ProductSort.PriceAsc => "COALESCE(MinPrice, BasePrice) ASC",
        ProductSort.PriceDesc => "COALESCE(MinPrice, BasePrice) DESC",
        ProductSort.Rating => "AvgRating DESC, ReviewCount DESC",
        _ => "CreatedAt DESC"
    };

    public async Task<PagedResult<ProductListingItem>> GetListingAsync(
        ProductListingFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var sql = $@"
            SELECT * FROM dbo.vw_ProductListing
            {ListingWhere}
            ORDER BY {OrderBy(filter.Sort)}, ProductId
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            OPTION (RECOMPILE);

            SELECT COUNT(*) FROM dbo.vw_ProductListing
            {ListingWhere}
            OPTION (RECOMPILE);";

        var p = new DynamicParameters();
        p.Add("CategorySlug", filter.CategorySlug, DbType.AnsiString, size: 120);
        p.Add("SearchPattern", ToLikePattern(filter.Search), DbType.String, size: 300);
        p.Add("InStockOnly", filter.InStockOnly, DbType.Boolean);
        p.Add("MinPrice", filter.MinPrice, DbType.Decimal);
        p.Add("MaxPrice", filter.MaxPrice, DbType.Decimal);
        p.Add("Offset", (page - 1) * pageSize, DbType.Int32);
        p.Add("PageSize", pageSize, DbType.Int32);

        await EnsureOpenAsync(ct);
        using var grid = await Connection.QueryMultipleAsync(
            new CommandDefinition(sql, p, Transaction, cancellationToken: ct));

        var items = (await grid.ReadAsync<ProductListingItem>()).AsList();
        var total = await grid.ReadSingleAsync<int>();
        return new PagedResult<ProductListingItem>(items, total);
    }

    public async Task<IReadOnlyList<ProductListingItem>> GetTopRatedAsync(int count, CancellationToken ct = default)
    {
        // Q2: ідемо за IX_ProductRatings_Rank, view додатково відсікає приховані/видалені товари.
        const string sql = @"
            SELECT TOP (@Count) l.*
            FROM dbo.ProductRatings r
            JOIN dbo.vw_ProductListing l ON l.ProductId = r.ProductId
            WHERE r.ReviewCount > 0
            ORDER BY r.AvgRating DESC, r.ReviewCount DESC, l.ProductId";

        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<ProductListingItem>(
            new CommandDefinition(sql, new { Count = count }, Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    // ---------- склад і алергени ----------

    public async Task ReplaceIngredientsAsync(Guid productId, IEnumerable<Guid> ingredientIds,
        CancellationToken ct = default)
    {
        RequireTransaction();
        await EnsureOpenAsync(ct);

        await Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.ProductIngredients WHERE ProductId = @ProductId",
            new { ProductId = productId }, Transaction, cancellationToken: ct));

        var rows = ingredientIds.Distinct().Select(id => new { ProductId = productId, IngredientId = id }).ToList();
        if (rows.Count == 0) return;

        await GuardAsync(async () => await Connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.ProductIngredients (ProductId, IngredientId) VALUES (@ProductId, @IngredientId)",
            rows, Transaction, cancellationToken: ct)));
    }

    public async Task ReplaceAllergensAsync(Guid productId,
        IEnumerable<(Guid AllergenId, AllergenPresence Presence)> allergens, CancellationToken ct = default)
    {
        RequireTransaction();
        await EnsureOpenAsync(ct);

        await Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.ProductAllergens WHERE ProductId = @ProductId",
            new { ProductId = productId }, Transaction, cancellationToken: ct));

        var rows = allergens
            .GroupBy(a => a.AllergenId)
            .Select(g => new { ProductId = productId, AllergenId = g.Key, Presence = g.Min(x => (byte)x.Presence) })
            .ToList();
        if (rows.Count == 0) return;

        await GuardAsync(async () => await Connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.ProductAllergens (ProductId, AllergenId, Presence) VALUES (@ProductId, @AllergenId, @Presence)",
            rows, Transaction, cancellationToken: ct)));
    }

    // ---------- Inbox: RatingSummaryChanged ----------

    public async Task<bool> ApplyRatingAsync(Guid eventId, Guid productId, decimal avgRating, int reviewCount,
        long ratingVersion, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var applied = await Connection.QuerySingleAsync<int>(new CommandDefinition(
            "dbo.usp_ProductRating_Apply",
            new
            {
                EventId = eventId,
                ProductId = productId,
                AvgRating = avgRating,
                ReviewCount = reviewCount,
                RatingVersion = ratingVersion
            },
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return applied == 1;
    }

    // Екранування спецсимволів LIKE (ESCAPE '\'), щоб «50%» шукалося буквально.
    private static string? ToLikePattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var escaped = search.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");
        return $"%{escaped}%";
    }
}
