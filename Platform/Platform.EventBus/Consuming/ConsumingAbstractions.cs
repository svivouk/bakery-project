using Microsoft.Extensions.DependencyInjection;
using Platform.EventBus.Events;

namespace Platform.EventBus.Consuming;

//Обробник одного типу події. Створюється в scope повідомлення, тому може брати з DI репозиторії, DbContext, кеш
public interface IEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}

/// <summary>Зв'язок «назва типу події → тип і виклик обробника».</summary>
public sealed record EventHandlerRegistration(
    Type EventType,
    Func<IServiceProvider, object, CancellationToken, Task> Invoke);

//Реєстр обробників сервісу. Ключ: BasicProperties.Type (назва класу події)
public sealed class EventHandlerRegistry(IEnumerable<EventHandlerRegistration> registrations)
{
    private readonly Dictionary<string, EventHandlerRegistration> _byType =
        registrations.ToDictionary(r => r.EventType.Name, StringComparer.Ordinal);

    public bool TryGet(string eventType, out EventHandlerRegistration registration) =>
        _byType.TryGetValue(eventType, out registration!);
}

public static class EventHandlerServiceCollectionExtensions
{
    //Реєструє обробник для типу події. Обробник створюється за scope на кожне повідомлення
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddScoped<IEventHandler<TEvent>, THandler>();
        services.AddSingleton(new EventHandlerRegistration(
            typeof(TEvent),
            (sp, @event, ct) => sp.GetRequiredService<IEventHandler<TEvent>>().HandleAsync((TEvent)@event, ct)));
        return services;
    }
}
