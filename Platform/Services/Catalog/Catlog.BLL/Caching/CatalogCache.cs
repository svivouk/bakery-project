using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

/// <summary>Єдина точка доступу до кешу Каталогу: дворівневий cache-aside (L1 IMemoryCache → L2 Redis → БД),
/// інвалідація, версія списків, структуровані логи й статистика.
/// <para>L1 живе в процесі й швидкий, але локальний для репліки; L2 (Redis) спільний. TTL L1 завжди коротший
/// за TTL L2, тому застарілий запис у пам'яті не переживе оновлення в Redis довше за кілька десятків секунд.</para> Збій Redis ніколи не ламає запит: читання йде в БД, а запис у БД, що вже відбувся, не відкочується.</summary>
public sealed class CatalogCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Верхня межа життя запису в L1.</summary>
    private static readonly TimeSpan MaxL1Ttl = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _cache;
    private readonly CacheStatistics _stats;
    private readonly ILogger<CatalogCache> _logger;

    public CatalogCache(IMemoryCache memory, IDistributedCache cache, CacheStatistics stats, ILogger<CatalogCache> logger)
    {
        _memory = memory;
        _cache = cache;
        _stats = stats;
        _logger = logger;
    }

    // ====================== читання (cache-aside) ======================

    /// <summary>Кеш → (промах) → джерело → запис у кеш. «Не знайдено» (null) у кеш не потрапляє.</summary>
    public async Task<T?> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T?>> factory,
        DistributedCacheEntryOptions options, CancellationToken ct = default) where T : class
    {
        var area = CatalogCacheKeys.AreaOf(key);
        var protocol = CacheCallContext.Protocol;   // "http" або "grpc": для окремого обліку й логів
        var tracker = CacheCallContext.Current;

        // L1: найшвидше, без мережі. Лише для типів з незмінними DTO (записи/колекції лише для читання).
        var l1Start = Stopwatch.GetTimestamp();
        var l1Found = _memory.TryGetValue(key, out object? local);
        _stats.Duration("get", "memory", Stopwatch.GetElapsedTime(l1Start).TotalMilliseconds);
        if (l1Found && local is T fromL1)
        {
            _stats.Hit(area, "l1", protocol);
            tracker?.Hit();
            TraceEvent("cache.hit", "memory", key);
            _logger.LogInformation("Cache hit L1 [{Protocol}] {CacheKey} (area {CacheArea})", protocol, key, area);
            return fromL1;
        }

        _stats.MemoryMiss(area, protocol);

        try
        {
            var redisStart = Stopwatch.GetTimestamp();
            var bytes = await _cache.GetAsync(key, ct);
            _stats.Duration("get", "redis", Stopwatch.GetElapsedTime(redisStart).TotalMilliseconds);
            if (bytes is not null)
            {
                var value = JsonSerializer.Deserialize<T>(bytes, Json);
                _stats.Hit(area, "l2", protocol);
                tracker?.Hit();
                TraceEvent("cache.hit", "redis", key);
                if (value is not null) SetLocal(key, value, options);   // L2 → L1 на зворотному шляху
                // Redis не повідомляє залишок TTL, тому логуємо TTL, з яким запис було створено (з політики).
                _logger.LogInformation(
                    "Cache hit L2 [{Protocol}] {CacheKey} (area {CacheArea}, size {SizeBytes} B, ttl {TtlSeconds}s)",
                    protocol, key, area, bytes.Length, TtlSeconds(options));
                return value;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _stats.Error(area);
            _logger.LogWarning(ex, "Cache read failed {CacheKey}, fallback to database", key);
        }

        _stats.Miss(area, protocol);
        tracker?.Miss();
        TraceEvent("cache.miss", "redis", key);
        _logger.LogInformation("Cache miss [{Protocol}] {CacheKey} (area {CacheArea}), loading from database",
            protocol, key, area);

        var created = await factory(ct);
        if (created is null) return null;

        // БД → L1 незалежно від успіху запису в Redis: так сервіс витримує його відмову.
        SetLocal(key, created, options);

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(created, Json);
            var setStart = Stopwatch.GetTimestamp();
            await _cache.SetAsync(key, payload, options, ct);
            _stats.Duration("set", "redis", Stopwatch.GetElapsedTime(setStart).TotalMilliseconds);
            _logger.LogInformation(
                "Cache set {CacheKey} (size {SizeBytes} B, ttl {TtlSeconds}s, sliding {Sliding})",
                key, payload.Length, TtlSeconds(options), options.SlidingExpiration.HasValue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _stats.Error(area);
            _logger.LogWarning(ex, "Cache write failed {CacheKey}", key);
        }

        return created;
    }

    // ====================== інвалідація ======================

    /// <summary>Видаляє ключі. Викликати <b>одразу після коміту</b> запису в БД, у тому самому запиті.</summary>
    public async Task InvalidateAsync(string reason, CancellationToken ct, params string?[] keys)
    {
        var removed = new List<string>();
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            try
            {
                _memory.Remove(key);   // L1 цієї репліки скидається завжди, навіть якщо Redis недоступний
                var removeStart = Stopwatch.GetTimestamp();
                await _cache.RemoveAsync(key, ct);
                _stats.Duration("remove", "redis", Stopwatch.GetElapsedTime(removeStart).TotalMilliseconds);
                removed.Add(key);
                _stats.Invalidation(CatalogCacheKeys.AreaOf(key));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Дані в БД уже збережені; застарілий запис проживе не довше за свій TTL.
                _stats.Error(CatalogCacheKeys.AreaOf(key));
                _logger.LogWarning(ex, "Cache invalidation failed {CacheKey}, reason {Reason}", key, reason);
            }
        }

        if (removed.Count > 0)
            _logger.LogInformation("Cache invalidated {CacheKeys}, reason {Reason}", removed, reason);
    }

    /// <summary>Масова інвалідація всіх лістингів і топу: міняємо токен-версію, яку містять їхні ключі.
    /// Старі записи стають недосяжними й самі зникають за коротким TTL, без пошуку за шаблоном (SCAN/KEYS).</summary>
    public async Task BumpListsVersionAsync(string reason, CancellationToken ct)
    {
        var newVersion = NewToken();
        try
        {
            await _cache.SetStringAsync(CatalogCacheKeys.ListsVersion, newVersion, ct);
            _stats.Invalidation("lists");
            _logger.LogInformation(
                "Cache invalidated all lists via {CacheKey}, new version {Version}, reason {Reason}",
                CatalogCacheKeys.ListsVersion, newVersion, reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _stats.Error("lists");
            _logger.LogWarning(ex, "Cache lists version bump failed, reason {Reason}", reason);
        }
    }

    /// <summary>Поточна версія списків (створюється при першому зверненні). Якщо Redis недоступний, повертає "0":
    /// кеш усе одно не працює, а запит піде в БД.</summary>
    public async Task<string> GetListsVersionAsync(CancellationToken ct)
    {
        try
        {
            var version = await _cache.GetStringAsync(CatalogCacheKeys.ListsVersion, ct);
            if (!string.IsNullOrEmpty(version)) return version;

            version = NewToken();
            await _cache.SetStringAsync(CatalogCacheKeys.ListsVersion, version, ct);
            return version;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _stats.Error("lists");
            _logger.LogWarning(ex, "Cache lists version unavailable");
            return "0";
        }
    }

    private void SetLocal(string key, object value, DistributedCacheEntryOptions l2)
    {
        var l2Ttl = l2.AbsoluteExpirationRelativeToNow ?? l2.SlidingExpiration ?? MaxL1Ttl;
        // L1 коротший за L2: не більше хвилини і не більше половини TTL у Redis.
        var l1Ttl = TimeSpan.FromTicks(Math.Min(MaxL1Ttl.Ticks, l2Ttl.Ticks / 2));
        var entry = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = l1Ttl,
            Size = 1   // AddMemoryCache(SizeLimit = 10_000) вимагає розмір кожного запису
        };
        entry.RegisterPostEvictionCallback((_, _, reason, _) => _stats.Eviction(reason.ToString()));
        _memory.Set(key, value, entry);
    }

    /// <summary>Подія на поточному span (серверний span HTTP/gRPC-запиту): у trace видно, звідки взято дані.</summary>
    private static void TraceEvent(string name, string level, string key) =>
        Activity.Current?.AddEvent(new ActivityEvent(name, tags: new ActivityTagsCollection
        {
            ["cache.level"] = level,
            ["cache.key"] = key
        }));

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private static int TtlSeconds(DistributedCacheEntryOptions o) =>
        (int)(o.AbsoluteExpirationRelativeToNow ?? o.SlidingExpiration ?? TimeSpan.Zero).TotalSeconds;
}
