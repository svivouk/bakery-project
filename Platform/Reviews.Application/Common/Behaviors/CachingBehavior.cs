using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Common.Behaviors;
public sealed class CachingBehavior<TRequest, TResponse>(
    IMemoryCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull, ICacheableQuery
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = request.GetCacheKey();
        if (key is null) return await next();

        if (cache.TryGetValue(key, out TResponse? cached) && cached is not null)
        {
            logger.LogDebug("Cache hit {CacheKey}", key);
            return cached;
        }

        var response = await next();
        cache.Set(key, response, request.CacheDuration);
        logger.LogDebug("Cache miss {CacheKey}: збережено на {TtlSeconds} с", key, (int)request.CacheDuration.TotalSeconds);
        return response;
    }
}