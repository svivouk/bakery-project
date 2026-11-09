using System.Security.Cryptography;
using System.Text;
using Catalog.Bll.Dto;

namespace Catalog.Bll.Caching;

/// <summary>Усі ключі кешу Каталогу в одному місці: читання й інвалідація використовують ті самі рядки.
/// Схема імен: <c>catalog:{сутність}:{ідентифікатор}</c>.</summary>
public static class CatalogCacheKeys
{
    public const string Ingredients = "catalog:ingredients";
    public const string Allergens = "catalog:allergens";
    public const string CategoriesActive = "catalog:categories:active";
    public const string CategoriesAll = "catalog:categories:all";

    /// <summary>Токен-версія всіх лістингів: зміна токена миттєво робить недосяжними всі старі ключі лістингів і топу.</summary>
    public const string ListsVersion = "catalog:lists:version";

    /// <summary>Група ключа для статистики: другий сегмент (product, listing, categories...).</summary>
    public static string AreaOf(string key)
    {
        var parts = key.Split(':');
        return parts.Length > 1 ? parts[1] : "other";
    }

    public static string Categories(bool activeOnly) => activeOnly ? CategoriesActive : CategoriesAll;

    /// <summary>Картка товару для вітрини (читається за slug, тому ключ за slug).</summary>
    public static string ProductDetails(string slug) => $"catalog:product:slug:{slug.Trim().ToLowerInvariant()}";

    public static string TopRated(string version, int count) => $"catalog:top-rated:v{version}:{count}";

    /// <summary>Лістинг: ключ містить хеш нормалізованих параметрів запиту (без самих значень, щоб ключ був коротким).</summary>
    public static string Listing(string version, ListingQuery query, int page, int pageSize)
    {
        var raw = string.Join('|',
            query.CategorySlug?.Trim().ToLowerInvariant() ?? "",
            query.Search?.Trim().ToLowerInvariant() ?? "",
            query.InStockOnly ? "1" : "0",
            query.MinPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            query.MaxPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            (int)query.Sort,
            page,
            pageSize);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
        return $"catalog:listing:v{version}:{hash}";
    }
}
