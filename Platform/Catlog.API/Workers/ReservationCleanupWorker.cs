using Catalog.Bll.Interfaces;

namespace Catalog.Api.Workers;

/// <summary>
/// Раз на хвилину повертає на склад прострочені резервації (usp_Stock_ReleaseExpired).
/// Операція ідемпотентна, тому безпечна при кількох інстансах і перезапусках.
/// Щоденне скидання норми сюди свідомо не входить: його запускають ззовні (POST /api/stock/maintenance/reset-daily).
/// </summary>
public sealed class ReservationCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReservationCleanupWorker> _logger;
    private readonly TimeSpan _interval;

    public ReservationCleanupWorker(IServiceScopeFactory scopes, IConfiguration configuration,
        ILogger<ReservationCleanupWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(5, configuration.GetValue("Stock:CleanupIntervalSeconds", 60)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // Власний scope → власний UnitOfWork і з'єднання на кожен прохід.
                    await using var scope = _scopes.CreateAsyncScope();
                    var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
                    var released = await stock.ReleaseExpiredAsync(stoppingToken);
                    if (released > 0)
                        _logger.LogInformation("Звільнено прострочених резервацій: {Count}", released);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Помилка очищення прострочених резервацій");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // нормальне завершення роботи застосунку
        }
    }
}
