using System.Text.Json;
using Catalog.Bll.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.Health;
public sealed class CacheHealthCheck(IDistributedCache cache, CacheStatistics statistics) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var s = statistics.Snapshot();
        var data = new Dictionary<string, object>
        {
            ["hits"] = s.Hits,
            ["l1Hits"] = s.L1Hits,
            ["l2Hits"] = s.Hits - s.L1Hits,
            ["misses"] = s.Misses,
            ["hitRatio"] = s.HitRatio,
            ["grpcHits"] = s.GrpcHits,
            ["grpcMisses"] = s.GrpcMisses,
            ["invalidations"] = s.Invalidations,
            ["errors"] = s.Errors,
            ["lastErrorUtc"] = s.LastErrorUtc?.ToString("O") ?? "—",
            ["areas"] = s.Areas.ToDictionary(
                a => a.Key,
                a => (object)new { a.Value.Hits, a.Value.L1Hits, a.Value.Misses, a.Value.Invalidations, a.Value.Errors })
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await cache.SetStringAsync("catalog:health:probe", "ok",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) }, cts.Token);
            var echo = await cache.GetStringAsync("catalog:health:probe", cts.Token);
            data["probeMs"] = sw.ElapsedMilliseconds;

            return echo == "ok"
                ? HealthCheckResult.Healthy("Redis доступний", data)
                : HealthCheckResult.Degraded("Redis повернув невірну відповідь", data: data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Redis недоступний, запити йдуть у БД", ex, data);
        }
    }
    public static Task WriteResponse(HttpContext http, HealthReport report)
    {
        http.Response.ContentType = "application/json; charset=utf-8";
        var entry = report.Entries.TryGetValue("cache", out var e) ? e : default;
        var payload = new
        {
            status = report.Status.ToString(),
            description = entry.Description,
            error = entry.Exception?.Message,
            data = entry.Data
        };
        return http.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
