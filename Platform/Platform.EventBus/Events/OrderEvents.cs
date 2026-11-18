namespace Platform.EventBus.Events;

public sealed record OrderLineSnapshot(Guid ProductId, Guid VariantId, int Quantity);

[EventRoute(Exchanges.OrdersEvents, RoutingKeys.OrderStatusChanged)]
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid CustomerId,
    string OldStatus,
    string NewStatus,
    IReadOnlyList<OrderLineSnapshot> Lines) : IntegrationEvent;