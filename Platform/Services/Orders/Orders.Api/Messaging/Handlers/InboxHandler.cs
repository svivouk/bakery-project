using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain;
using Orders.Domain.Messaging;
using Platform.EventBus.Consuming;
using Platform.EventBus.Events;

namespace Orders.Api.Messaging.Handlers;

public abstract class InboxHandler<TEvent>(IUnitOfWork uow, ILogger logger) : IEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
    protected IUnitOfWork Uow { get; } = uow;
    protected ILogger Logger { get; } = logger;

    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        if (await Uow.Inbox.GetByIdAsync(@event.EventId, ct) is not null)
        {
            Logger.LogInformation("Duplicate event {EventId} skipped", @event.EventId);
            return;
        }

        try
        {
            await ApplyAsync(@event, ct);
        }
        catch (DomainException ex)
        {
            throw new PermanentFailureException(ex.Message, ex);
        }

        await Uow.Inbox.AddAsync(new InboxMessage(@event.EventId, typeof(TEvent).Name), ct);

        try
        {
            await Uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            
            Logger.LogInformation("Duplicate event {EventId} skipped (concurrent processing)", @event.EventId);
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2627 or 2601 };

    protected abstract Task ApplyAsync(TEvent @event, CancellationToken ct);
}