namespace Platform.EventBus.Events;

[EventRoute(Exchanges.CatalogEvents, RoutingKeys.ProductPriceChanged)]
public sealed record ProductPriceChangedEvent(
    Guid ProductId,
    Guid? VariantId,
    string? ProductName,
    decimal? OldPrice,
    decimal NewPrice) : IntegrationEvent;

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
