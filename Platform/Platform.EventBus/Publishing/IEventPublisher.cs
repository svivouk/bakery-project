using Platform.EventBus.Events;

namespace Platform.EventBus.Publishing;

public interface IEventPublisher
{
    //Публікує подію за маршрутом з атрибута [EventRoute] на типі події
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent;

    //Публікує подію у вказаний exchange з явним routing key
    Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent;
}
