using RabbitMQ.Client;

namespace Platform.EventBus;
public static class MessagingTopology
{
    public static async Task DeclareAsync(IChannel ch, CancellationToken ct = default)
    {
        await ch.ExchangeDeclareAsync(Exchanges.CatalogEvents, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync(Exchanges.OrdersEvents, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync(Exchanges.CatalogDlx, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync(Exchanges.OrdersDlx, ExchangeType.Direct, durable: true, cancellationToken: ct);

        // ---- черги споживачів подій каталогу ----
        await DeclareConsumerQueueAsync(ch, Queues.OrdersCatalogSync,
            Exchanges.CatalogEvents, Exchanges.CatalogDlx,
            [RoutingKeys.ProductPriceChanged, RoutingKeys.ProductRenamed, RoutingKeys.ProductDeleted], ct);

        await DeclareConsumerQueueAsync(ch, Queues.ReviewsCatalogSync,
            Exchanges.CatalogEvents, Exchanges.CatalogDlx,
            [RoutingKeys.ProductRenamed, RoutingKeys.ProductDeleted], ct);

        // ---- черга споживача подій замовлень (приклад: Каталог реагує на замовлення) ----
        await DeclareConsumerQueueAsync(ch, Queues.CatalogOrdersSync,
            Exchanges.OrdersEvents, Exchanges.OrdersDlx,
            ["order.*"], ct);
    }

    /// <summary>Durable-черга + DLQ + bindings. Для кожної черги ключ dead-letter = її назва.</summary>
    private static async Task DeclareConsumerQueueAsync(
        IChannel ch, string queue, string exchange, string dlx, string[] bindingKeys, CancellationToken ct)
    {
        var dlq = Queues.DeadLetter(queue);

        await ch.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlx,
                ["x-dead-letter-routing-key"] = queue
            },
            cancellationToken: ct);

        await ch.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await ch.QueueBindAsync(dlq, dlx, routingKey: queue, cancellationToken: ct);

        foreach (var key in bindingKeys)
            await ch.QueueBindAsync(queue, exchange, routingKey: key, cancellationToken: ct);
    }
}
