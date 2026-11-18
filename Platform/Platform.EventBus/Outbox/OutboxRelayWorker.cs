using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Publishing;

namespace Platform.EventBus.Outbox;

public sealed class OutboxRelayWorker(
    IServiceScopeFactory scopes,
    IEventPublisher publisher,
    IOutboxEventMapper mapper,
    ILogger<OutboxRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var handled = await RelayBatchAsync(stoppingToken);
                if (handled == 0)
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox relay failed, retrying in {Delay}", ErrorDelay);
                try { await Task.Delay(ErrorDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<int> RelayBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var pending = await store.GetPendingAsync(BatchSize, ct);
        if (pending.Count == 0) return 0;

        var done = new List<long>(pending.Count);
        try
        {
            foreach (var record in pending)
            {
                var @event = mapper.Map(record);
                if (@event is null)
                {
                    logger.LogDebug("Outbox {OutboxId} {EventType} is internal, skipped", record.Id, record.EventType);
                }
                else
                {
                    await publisher.PublishAsync(@event, ct);          
                }
                done.Add(record.Id);
            }
        }
        finally
        {
            if (done.Count > 0)
                await store.MarkProcessedAsync(done, CancellationToken.None);
        }

        return done.Count;
    }
}

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxRelay<TStore, TMapper>(this IServiceCollection services)
        where TStore : class, IOutboxStore
        where TMapper : class, IOutboxEventMapper
    {
        services.AddScoped<IOutboxStore, TStore>();
        services.AddSingleton<IOutboxEventMapper, TMapper>();
        services.AddHostedService<OutboxRelayWorker>();
        return services;
    }
}
