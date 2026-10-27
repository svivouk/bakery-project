namespace Orders.Domain.Orders;

public readonly record struct OrderLine(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantName,
    decimal UnitPrice,
    int Quantity);
