using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Common.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse>(
    IDistributedCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull, ICacheableQuery
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = request.GetCacheKey();
        if (key is null) return await next();

        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is not null && JsonSerializer.Deserialize<TResponse>(bytes, Json) is { } cached)
            {
                logger.LogDebug("Cache hit {CacheKey}", key);
                return cached;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Кеш недоступний для читання {CacheKey}", key);
        }

        var response = await next();
        if (response is null) return response;

        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(response, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = request.CacheDuration }, ct);
            logger.LogDebug("Cache miss {CacheKey}: збережено на {TtlSeconds} с", key, (int)request.CacheDuration.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не вдалося записати в кеш {CacheKey}", key);
        }

        return response;
    }
}