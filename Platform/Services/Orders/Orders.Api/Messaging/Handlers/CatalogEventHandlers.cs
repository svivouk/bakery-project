using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Catalog;
using Platform.EventBus.Events;

namespace Orders.Api.Messaging.Handlers;

public sealed class ProductCreatedHandler(IUnitOfWork uow, ILogger<ProductCreatedHandler> logger)
    : InboxHandler<ProductCreatedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(ProductCreatedEvent e, CancellationToken ct)
    {
        if (await Uow.ProductRefs.GetByIdAsync(e.ProductId, ct) is not null)
        {
            Logger.LogInformation("Product {ProductId} already exists in Orders, creation skipped", e.ProductId);
            return;
        }

        await Uow.ProductRefs.AddAsync(new ProductRef(e.ProductId, e.Name, isActive: true, e.SourceVersion), ct);
        Logger.LogInformation("Product {ProductId} created in Orders as {Name}", e.ProductId, e.Name);
    }
}
public sealed class ProductPriceChangedHandler(IUnitOfWork uow, ILogger<ProductPriceChangedHandler> logger)
    : InboxHandler<ProductPriceChangedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(ProductPriceChangedEvent e, CancellationToken ct)
    {
        if (e.VariantId is null)
        {
            Logger.LogDebug("Base price of product {ProductId} changed: Orders keep prices per variant, nothing to update",
                e.ProductId);
            return;
        }

        var variant = await Uow.VariantRefs.GetByIdAsync(e.VariantId.Value, ct);
        if (variant is null)
        {
            Logger.LogWarning("Variant {VariantId} of product {ProductId} is unknown in Orders, price update skipped",
                e.VariantId, e.ProductId);
            return;
        }

        if (!variant.ChangePrice(e.NewPrice, e.SourceVersion))
            Logger.LogInformation("Stale price event {EventId} for variant {VariantId} ignored (version {Version})",
                e.EventId, e.VariantId, e.SourceVersion);
        else
            Logger.LogInformation("Variant {VariantId} price set to {Price}", e.VariantId, e.NewPrice);
    }
}

public sealed class ProductRenamedHandler(IUnitOfWork uow, ILogger<ProductRenamedHandler> logger)
    : InboxHandler<ProductRenamedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(ProductRenamedEvent e, CancellationToken ct)
    {
        var product = await Uow.ProductRefs.GetByIdAsync(e.ProductId, ct);
        if (product is null)
        {
            Logger.LogWarning("Product {ProductId} is unknown in Orders, rename skipped", e.ProductId);
            return;
        }

        if (!product.Rename(e.NewName, e.SourceVersion))
            Logger.LogInformation("Stale rename event {EventId} for product {ProductId} ignored (version {Version})",
                e.EventId, e.ProductId, e.SourceVersion);
        else
            Logger.LogInformation("Product {ProductId} renamed to {Name}", e.ProductId, e.NewName);
    }
}
public sealed class ProductDeletedHandler(IUnitOfWork uow, ILogger<ProductDeletedHandler> logger)
    : InboxHandler<ProductDeletedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(ProductDeletedEvent e, CancellationToken ct)
    {
        var product = await Uow.ProductRefs.GetByIdAsync(e.ProductId, ct);
        if (product is null)
        {
            Logger.LogWarning("Product {ProductId} is unknown in Orders, deactivation skipped", e.ProductId);
            return;
        }

        if (!product.Deactivate(e.SourceVersion))
            Logger.LogInformation("Stale deactivation event {EventId} for product {ProductId} ignored (version {Version})",
                e.EventId, e.ProductId, e.SourceVersion);
        else
            Logger.LogInformation("Product {ProductId} deactivated", e.ProductId);
    }
}
