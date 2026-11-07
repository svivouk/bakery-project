using Orders.Domain;

namespace Orders.Domain.Carts;

/// Позиція кошика. ProductId/VariantId — ідентифікатори іншого контексту (Каталог),
/// тому зовнішнього ключа на довідник у БД немає: копії можуть ще не прийти.
public sealed class CartItem : Entity
{
    private CartItem() { } 

    internal CartItem(Guid productId, Guid variantId, int quantity)
    {
        ProductId = Guard.NotEmpty(productId, "Товар");
        VariantId = Guard.NotEmpty(variantId, "Варіант");
        Quantity = Guard.Positive(quantity, "Кількість");
        AddedAt = DateTime.UtcNow;
    }

    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public int Quantity { get; private set; }
    public DateTime AddedAt { get; private set; }

    internal void Increase(int by) => Quantity = checked(Quantity + Guard.Positive(by, "Кількість"));

    internal void SetQuantity(int quantity) => Quantity = Guard.Positive(quantity, "Кількість");
}
