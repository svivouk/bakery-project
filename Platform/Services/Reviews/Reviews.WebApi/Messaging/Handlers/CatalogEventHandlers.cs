using Platform.EventBus.Consuming;
using Platform.EventBus.Events;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.WebApi.Messaging.Handlers;

public sealed class ProductRenamedHandler(
    IUnitOfWork uow,
    IInbox inbox,
    IProductNameVersionStore versions,
    IReviewRepository reviews,
    ILogger<ProductRenamedHandler> logger) : IEventHandler<ProductRenamedEvent>
{
    public Task HandleAsync(ProductRenamedEvent e, CancellationToken ct) =>
        uow.ExecuteAsync(async token =>
        {
            if (!await inbox.TryRegisterAsync(e.EventId, nameof(ProductRenamedEvent), token))
            {
                logger.LogInformation("Duplicate event {EventId} skipped", e.EventId);
                return;
            }

            if (!await versions.TryAdvanceAsync(e.ProductId, e.SourceVersion, token))
            {
                logger.LogInformation("Stale rename event {EventId} for product {ProductId} ignored (version {Version})",
                    e.EventId, e.ProductId, e.SourceVersion);
                return;
            }

            try
            {
                var updated = await reviews.RenameProductAsync(e.ProductId, e.NewName, token);
                logger.LogInformation("Product {ProductId} renamed to {Name} in {Count} reviews",
                    e.ProductId, e.NewName, updated);
            }
            catch (DomainException ex)
            {
                throw new PermanentFailureException(ex.Message, ex);
            }
        }, ct);
}

public sealed class ProductDeletedHandler(
    IUnitOfWork uow,
    IInbox inbox,
    ILogger<ProductDeletedHandler> logger) : IEventHandler<ProductDeletedEvent>
{
    public Task HandleAsync(ProductDeletedEvent e, CancellationToken ct) =>
        uow.ExecuteAsync(async token =>
        {
            if (!await inbox.TryRegisterAsync(e.EventId, nameof(ProductDeletedEvent), token))
            {
                logger.LogInformation("Duplicate event {EventId} skipped", e.EventId);
                return;
            }

            logger.LogInformation("Product {ProductId} is no longer available; its reviews are kept", e.ProductId);
        }, ct);
}

