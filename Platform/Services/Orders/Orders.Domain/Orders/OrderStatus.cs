namespace Orders.Domain.Orders;

public enum OrderStatus
{
    New,
    Confirmed,
    Preparing,
    Ready,
    Completed,
    Cancelled
}

public enum DeliveryType
{
    Delivery,
    Pickup
}

public enum DeliveryStatus
{
    Pending,
    Assigned,
    OutForDelivery,
    Delivered,
    Failed,
    Cancelled
}
