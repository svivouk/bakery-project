using Orders.Domain;

namespace Orders.Domain.Orders;

///  Позиція замовлення зі знімком назви й ціни: зміни в Каталозі не переписують історію.
/// Після створення не змінюється. 
public sealed class OrderItem : Entity
{
    public const int ProductNameMaxLength = 200;
    public const int VariantNameMaxLength = 100;

    private OrderItem() { } // для EF Core

    internal OrderItem(OrderLine line)
    {
        ProductId = Guard.NotEmpty(line.ProductId, "Товар");
        VariantId = Guard.NotEmpty(line.VariantId, "Варіант");
        ProductName = Guard.Required(line.ProductName, "Назва товару", ProductNameMaxLength);
        VariantName = Guard.Required(line.VariantName, "Назва варіанта", VariantNameMaxLength);
        UnitPrice = Guard.Money(line.UnitPrice, "Ціна", precision: 10);
        Quantity = Guard.Positive(line.Quantity, "Кількість");
        LineTotal = UnitPrice * Quantity;
    }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string VariantName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }
}
