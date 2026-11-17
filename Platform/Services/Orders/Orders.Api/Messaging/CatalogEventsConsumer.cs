using Platform.EventBus;
using Platform.EventBus.Consuming;
using RabbitMQ.Client;

namespace Orders.Api.Messaging;
public sealed class CatalogEventsConsumer(
    IConnection connection, EventMessageProcessor processor, ILogger<CatalogEventsConsumer> logger)
    : QueueConsumer(connection, processor, logger, Queues.OrdersCatalogSync);
