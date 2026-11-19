namespace Platform.EventBus.Events;

[EventRoute(Exchanges.CatalogEvents, RoutingKeys.ProductCreated)]
public sealed record ProductCreatedEvent(
    Guid ProductId,
    string Name,
    string Slug,
    decimal BasePrice) : IntegrationEvent;

[EventRoute(Exchanges.CatalogEvents, RoutingKeys.ProductPriceChanged)]
public sealed record ProductPriceChangedEvent(
    Guid ProductId,
    Guid? VariantId,
    string? ProductName,
    decimal? OldPrice,
    decimal NewPrice,
    string? VariantName = null) : IntegrationEvent;

[EventRoute(Exchanges.CatalogEvents, RoutingKeys.ProductRenamed)]
public sealed record ProductRenamedEvent(
    Guid ProductId,
    string OldName,
    string NewName,
    string Slug) : IntegrationEvent;

[EventRoute(Exchanges.CatalogEvents, RoutingKeys.ProductDeleted)]
public sealed record ProductDeletedEvent(
    Guid ProductId,
    string? ProductName) : IntegrationEvent;
