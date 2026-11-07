using Orders.Domain;

namespace Orders.Domain.Carts;

/// Корінь агрегату «Кошик»: один активний кошик на клієнта.
/// Кожна зміна позицій оновлює <see cref="UpdatedAt"/>, тому змінюється й RowVersion кошика
public sealed class Cart : Entity
{
    private readonly List<CartItem> _items = new();

    private Cart() { } // для EF Core

    public Cart(Guid customerId)
    {
        CustomerId = Guard.NotEmpty(customerId, "Клієнт");
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<CartItem> Items => _items;

    ///  Додає варіант у кошик; якщо він уже є, збільшує кількість (UQ_CartItems_Variant). 
    public CartItem AddItem(Guid productId, Guid variantId, int quantity)
    {
        Guard.Positive(quantity, "Кількість");
        var item = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (item is null)
        {
            item = new CartItem(productId, variantId, quantity);
            _items.Add(item);
        }
        else
        {
            item.Increase(quantity);
        }
        Touch();
        return item;
    }

    public void ChangeQuantity(Guid variantId, int quantity)
    {
        Guard.Positive(quantity, "Кількість");
        Find(variantId).SetQuantity(quantity);
        Touch();
    }

    public void RemoveItem(Guid variantId)
    {
        _items.Remove(Find(variantId));
        Touch();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Touch();
    }

    private CartItem Find(Guid variantId)
        => _items.FirstOrDefault(i => i.VariantId == variantId)
           ?? throw new DomainException("Позицію не знайдено в кошику.");

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
