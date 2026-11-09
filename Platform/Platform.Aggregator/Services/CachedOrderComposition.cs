using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Services;

/// <summary>Кеш складених відповідей у Redis над <see cref="OrderCompositionService"/>.
/// Кеш перевіряється ДО паралельних викликів сервісів; часткові відповіді (є Warnings) і «не знайдено» не кешуються.
/// TTL 30 с свідомо коротший за кеші окремих сервісів (Каталог: хвилини), щоб зібрана відповідь не виглядала
/// «свіжішою», ніж дані, з яких її зібрано.</summary>
public sealed class CachedOrderComposition
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OrderCompositionService _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedOrderComposition> _logger;
    private readonly TimeSpan _ttl;

    public CachedOrderComposition(OrderCompositionService inner, IDistributedCache cache,
        IConfiguration configuration, ILogger<CachedOrderComposition> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Cache:OrderFullTtlSeconds", 30), 5, 60));
    }

    public static string Key(Guid orderId) => $"aggregator:order:{orderId:D}:full";

    public async Task<FullOrderDto?> GetFullOrderAsync(Guid orderId, CancellationToken ct)
    {
        var key = Key(orderId);

        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes is not null)
            {
                var cached = JsonSerializer.Deserialize<FullOrderDto>(bytes, Json);
                if (cached is not null)
                {
                    _logger.LogInformation("Aggregator cache hit {CacheKey} (size {SizeBytes} B)", key, bytes.Length);
                    return cached;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Aggregator cache read failed {CacheKey}, composing without cache", key);
        }

        _logger.LogInformation("Aggregator cache miss {CacheKey}", key);
        var result = await _inner.GetFullOrderAsync(orderId, ct);

        if (result is null || result.Warnings.Count > 0)
        {
            if (result is not null)
                _logger.LogInformation("Aggregator response not cached {CacheKey}: partial ({Warnings})",
                    key, string.Join("; ", result.Warnings));
            return result;
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(result, Json);
            await _cache.SetAsync(key, payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _ttl }, ct);
            _logger.LogInformation("Aggregator cache set {CacheKey} (size {SizeBytes} B, ttl {TtlSeconds}s)",
                key, payload.Length, (int)_ttl.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Aggregator cache write failed {CacheKey}", key);
        }

        return result;
    }

    /// <summary>Скидає складену відповідь замовлення (викликати, коли відомо про зміну вихідних даних).</summary>
    public async Task InvalidateAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var key = Key(orderId);
        try
        {
            await _cache.RemoveAsync(key, ct);
            _logger.LogInformation("Aggregator cache invalidated {CacheKey}, reason {Reason}", key, reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Aggregator cache invalidation failed {CacheKey}", key);
        }
    }
}
