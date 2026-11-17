using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>Оголошує топологію під час старту застосунку (до того, як почнуть публікувати чи споживати).</summary>
public sealed class TopologyInitializer(IConnection connection, ILogger<TopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await MessagingTopology.DeclareAsync(channel, cancellationToken);
        logger.LogInformation("RabbitMQ topology declared");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class EventBusServiceCollectionExtensions
{
    /// <summary>Реєструє оголошення топології при старті. Викликати після builder.AddRabbitMQClient("messaging").</summary>
    public static IServiceCollection AddEventBusTopology(this IServiceCollection services) =>
        services.AddHostedService<TopologyInitializer>();
}

public static class EventBusHostingExtensions
{
    /// <summary>
    /// Єдина точка підключення до брокера для сервісу: IConnection (Aspire, ім'я ресурсу "messaging"),
    /// health check, телеметрія і оголошення топології при старті.
    /// </summary>
    public static IHostApplicationBuilder AddEventBus(this IHostApplicationBuilder builder)
    {
        builder.AddRabbitMQClient("messaging");
        builder.Services.AddEventBusTopology();
        return builder;
    }
}
