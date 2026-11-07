using Catalog.Domain.Constants;

namespace Catalog.Domain.Models;

public class StockItem : AuditableEntity
{    public Guid VariantId { get; set; }
    public int QuantityAvailable { get; set; }
    public int DailyLimit { get; set; }
}

public class StockReservation
{
    public Guid ReservationId { get; set; }
    public Guid VariantId { get; set; }
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
