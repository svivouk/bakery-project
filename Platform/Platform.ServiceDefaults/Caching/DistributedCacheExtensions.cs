using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task<T?> GetOrCreateAsync<T>(
        this IDistributedCache cache, string key,
        Func<CancellationToken, Task<T?>> factory,
        DistributedCacheEntryOptions options,
        ILogger logger, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is not null)
            {
                logger.LogDebug("Cache hit {CacheKey}", key);
                return JsonSerializer.Deserialize<T>(bytes, Json);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Кеш недоступний для читання {CacheKey}", key);
        }

        logger.LogDebug("Cache miss {CacheKey}", key);
        var value = await factory(ct);
        if (value is null) return null;                       // «не знайдено» не кешуємо

        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, Json), options, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не вдалося записати в кеш {CacheKey}", key);
        }

        return value;
    }

    public static async Task InvalidateAsync(
        this IDistributedCache cache, ILogger logger, CancellationToken ct, params string[] keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await cache.RemoveAsync(key, ct);
                logger.LogDebug("Cache invalidated {CacheKey}", key);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Не вдалося інвалідувати кеш {CacheKey}", key);
            }
        }
    }
}

public static class CachePolicies
{
    public static readonly DistributedCacheEntryOptions Categories = new()
    { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };

    public static readonly DistributedCacheEntryOptions ProductCard = new()
    { SlidingExpiration = TimeSpan.FromMinutes(10), AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };

    public static readonly DistributedCacheEntryOptions Listing = new()
    { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };

    public static readonly DistributedCacheEntryOptions RatingSummary = new()
    { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    public static readonly DistributedCacheEntryOptions ReviewList = new()
    { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) };
}