using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Platform.EventBus.Monitoring;

public sealed class DeadLetterMonitorOptions
{
    public List<string> Queues { get; set; } = [];

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);
}

public sealed class DeadLetterDepthMonitor : BackgroundService
{
    public const string MeterName = "Platform.Messaging";

    private readonly IConnection _connection;
    private readonly ILogger<DeadLetterDepthMonitor> _logger;
    private readonly DeadLetterMonitorOptions _options;
    private readonly ConcurrentDictionary<string, long> _depth = new();

    public DeadLetterDepthMonitor(
        IConnection connection,
        IMeterFactory meterFactory,
        IOptions<DeadLetterMonitorOptions> options,
        ILogger<DeadLetterDepthMonitor> logger)
    {
        _connection = connection;
        _logger = logger;
        _options = options.Value;

        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge(
            "eventbus.dlq.depth",
            () => _depth.Select(kv => new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("queue", kv.Key))),
            unit: "{message}",
            description: "Кількість повідомлень у dead letter queue (потребують ручного розслідування)");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Queues.Count == 0) return;

        using var timer = new PeriodicTimer(_options.Interval);
        do
        {
            foreach (var queue in _options.Queues)
                await PollAsync(Queues.DeadLetter(queue), stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private async Task PollAsync(string dlq, CancellationToken ct)
    {
        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);
            var info = await channel.QueueDeclarePassiveAsync(dlq, ct);

            var previous = _depth.TryGetValue(dlq, out var p) ? p : -1;
            _depth[dlq] = info.MessageCount;

            if (info.MessageCount > 0 && info.MessageCount != previous)
                _logger.LogWarning(
                    "Dead letter queue {Queue} contains {Depth} message(s): manual investigation is required",
                    dlq, info.MessageCount);
            else if (info.MessageCount == 0 && previous > 0)
                _logger.LogInformation("Dead letter queue {Queue} is empty again", dlq);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read depth of {Queue}", dlq);
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}

public static class DeadLetterMonitoringExtensions
{
    public static IServiceCollection AddDeadLetterMonitoring(this IServiceCollection services, params string[] consumerQueues)
    {
        services.Configure<DeadLetterMonitorOptions>(o => o.Queues.AddRange(consumerQueues));
        services.AddHostedService<DeadLetterDepthMonitor>();
        return services;
    }
}
