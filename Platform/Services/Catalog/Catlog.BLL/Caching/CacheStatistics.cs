using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Catalog.Bll.Caching;

/// <summary>Лічильники кешу: і метрики OpenTelemetry (Meter "Catalog.Cache", видно в Aspire Dashboard → Metrics),
/// і знімок для health check. Лічильники живуть у пам'яті процесу, тому після рестарту сервісу починаються з нуля,
/// а кожна репліка рахує свої.</summary>
public sealed class CacheStatistics
{
    public const string MeterName = "Catalog.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _evictions;
    private readonly Histogram<double> _duration;

    private readonly ConcurrentDictionary<string, AreaCounters> _areas = new();
    private long _lastErrorTicks;
    private long _grpcHits;
    private long _grpcMisses;

    public CacheStatistics(IMeterFactory meterFactory, IMemoryCache memory)
    {
        var meter = meterFactory.Create(MeterName);

        // Атрибут cache.level: "memory" (L1, IMemoryCache) або "redis" (L2). Частка попадань по рівню:
        // hits{level} / (hits{level} + misses{level}). Промах L1 з наступним попаданням у Redis рахується як
        // miss(memory) + hit(redis), а промах Redis (тобто похід у БД) як miss(redis).
        _hits = meter.CreateCounter<long>("cache.hits", description: "Кількість попадань у кеш");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Кількість промахів кешу");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", description: "Кількість інвалідацій");
        _errors = meter.CreateCounter<long>("cache.errors", description: "Помилки Redis (кеш пропущено)");
        _evictions = meter.CreateCounter<long>("cache.evictions",
            description: "Витіснення записів з L1 (за причиною: Expired, Capacity, Removed...)");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", unit: "ms",
            description: "Тривалість операцій з кешем (get/set/remove)");

        // Розмір L1: потребує MemoryCacheOptions.TrackStatistics = true (див. Program.cs).
        meter.CreateObservableGauge("cache.memory.entries",
            () => memory.GetCurrentStatistics()?.CurrentEntryCount ?? 0,
            description: "Кількість записів у L1 (пам'ять процесу)");
        meter.CreateObservableGauge("cache.memory.size",
            () => memory.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0,
            description: "Оцінений розмір L1 в умовних одиницях (1 запис = 1)");
    }

    /// <summary>Попадання: tier = "l1" (пам'ять процесу) або "l2" (Redis). У загальний Hits входять обидва.</summary>
    public void Hit(string area, string tier, string protocol)
    {
        var c = Area(area);
        Interlocked.Increment(ref c.Hits);
        if (tier == "l1") Interlocked.Increment(ref c.L1Hits);
        if (protocol == "grpc") Interlocked.Increment(ref _grpcHits);
        _hits.Add(1, new KeyValuePair<string, object?>("area", area),
            new KeyValuePair<string, object?>("cache.level", tier == "l1" ? "memory" : "redis"),
            new KeyValuePair<string, object?>("protocol", protocol));
    }

    public void Miss(string area, string protocol)
    {
        Interlocked.Increment(ref Area(area).Misses);
        if (protocol == "grpc") Interlocked.Increment(ref _grpcMisses);
        _misses.Add(1, new KeyValuePair<string, object?>("area", area),
            new KeyValuePair<string, object?>("cache.level", "redis"),
            new KeyValuePair<string, object?>("protocol", protocol));
    }

    /// <summary>Промах L1 (лише метрика; у загальний Misses потрапляє тільки остаточний промах, що йде в БД).</summary>
    public void MemoryMiss(string area, string protocol) =>
        _misses.Add(1, new KeyValuePair<string, object?>("area", area),
            new KeyValuePair<string, object?>("cache.level", "memory"),
            new KeyValuePair<string, object?>("protocol", protocol));

    public void Eviction(string reason) =>
        _evictions.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Duration(string operation, string level, double milliseconds) =>
        _duration.Record(milliseconds, new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("cache.level", level));

    public void Invalidation(string area, int keys = 1)
    {
        Interlocked.Add(ref Area(area).Invalidations, keys);
        _invalidations.Add(keys, new KeyValuePair<string, object?>("area", area));
    }

    public void Error(string area)
    {
        Interlocked.Increment(ref Area(area).Errors);
        Interlocked.Exchange(ref _lastErrorTicks, DateTime.UtcNow.Ticks);
        _errors.Add(1, new KeyValuePair<string, object?>("area", area));
    }

    public CacheStatisticsSnapshot Snapshot()
    {
        var areas = _areas.ToDictionary(
            kv => kv.Key,
            kv => new AreaSnapshot(
                Interlocked.Read(ref kv.Value.Hits),
                Interlocked.Read(ref kv.Value.L1Hits),
                Interlocked.Read(ref kv.Value.Misses),
                Interlocked.Read(ref kv.Value.Invalidations),
                Interlocked.Read(ref kv.Value.Errors)));

        var hits = areas.Values.Sum(a => a.Hits);
        var misses = areas.Values.Sum(a => a.Misses);
        var ticks = Interlocked.Read(ref _lastErrorTicks);

        return new CacheStatisticsSnapshot(
            hits, areas.Values.Sum(a => a.L1Hits), misses,
            areas.Values.Sum(a => a.Invalidations),
            areas.Values.Sum(a => a.Errors),
            hits + misses == 0 ? 0 : Math.Round((double)hits / (hits + misses), 4),
            ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc),
            areas,
            Interlocked.Read(ref _grpcHits),
            Interlocked.Read(ref _grpcMisses));
    }

    private AreaCounters Area(string area) => _areas.GetOrAdd(area, _ => new AreaCounters());

    private sealed class AreaCounters
    {
        public long Hits;
        public long L1Hits;
        public long Misses;
        public long Invalidations;
        public long Errors;
    }
}

public sealed record AreaSnapshot(long Hits, long L1Hits, long Misses, long Invalidations, long Errors);

public sealed record CacheStatisticsSnapshot(
    long Hits,
    long L1Hits,
    long Misses,
    long Invalidations,
    long Errors,
    double HitRatio,
    DateTime? LastErrorUtc,
    IReadOnlyDictionary<string, AreaSnapshot> Areas,
    long GrpcHits,
    long GrpcMisses);
