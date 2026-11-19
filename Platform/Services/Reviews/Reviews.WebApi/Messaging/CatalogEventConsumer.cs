using Platform.EventBus;
using Platform.EventBus.Consuming;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Reviews.WebApi.Messaging;

public sealed class CatalogEventsConsumer(
    IConnection connection, EventMessageProcessor processor, ILogger<CatalogEventsConsumer> logger)
    : QueueConsumer(connection, processor, logger, Queues.ReviewsCatalogSync);



