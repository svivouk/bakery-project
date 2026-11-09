using System.Diagnostics;
using Catalog.Bll.Caching;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;

namespace Catalog.Api.Workers;

public sealed class CacheWarmupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CacheWarmupWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _topCount;

    public CacheWarmupWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<CacheWarmupWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("Cache:WarmupIntervalSeconds", 60)));
        _topCount = Math.Clamp(configuration.GetValue("Cache:WarmupTopProducts", 10), 1, 50);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WarmAsync("startup", stoppingToken);   

            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await WarmAsync("periodic", stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WarmAsync(string trigger, CancellationToken ct)
    {
        CacheCallContext.Begin("warmup");   
        var sw = Stopwatch.StartNew();
        var warmed = 0;
        var failed = 0;

        await using var scope = _scopes.CreateAsyncScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        async Task Step(string name, Func<Task> action)
        {
            try
            {
                await action();
                warmed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger.LogWarning(ex, "Cache warmup step failed {Step}", name);
            }
        }

        await Step("categories", async () => { await categories.ListAsync(true, ct); await categories.ListAsync(false, ct); });
        await Step("ingredients", () => products.GetIngredientsAsync(ct));
        await Step("allergens", () => products.GetAllergensAsync(ct));
        await Step("listing", () => products.GetListingAsync(new ListingQuery { Page = 1, PageSize = 20 }, ct));

        IReadOnlyList<ProductListItemDto> top = [];
        await Step("top-rated", async () => top = await products.GetTopRatedAsync(_topCount, ct));

        foreach (var item in top)
            await Step($"product:{item.Slug}", () => products.GetDetailsBySlugAsync(item.Slug, ct));

        _logger.LogInformation(
            "Cache warmup {Trigger} finished: {Warmed} steps ok, {Failed} failed, {ElapsedMs} ms",
            trigger, warmed, failed, sw.ElapsedMilliseconds);
    }
}
