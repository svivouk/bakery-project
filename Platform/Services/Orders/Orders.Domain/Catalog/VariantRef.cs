using Orders.Domain;

namespace Orders.Domain.Catalog;

/// Локальна копія варіанта товару (ціна для кошика). Окрема від <see cref="ProductRef"/>,
/// щоб назва товару не повторювалася в кожному варіанті (3НФ). Оновлюється лише подіями.
public sealed class VariantRef
{
    public const int NameMaxLength = 100;

    private VariantRef() { } // для EF Core

    public VariantRef(Guid variantId, Guid productId, string name, decimal price, bool isActive, long sourceVersion)
    {
        Id = Guard.NotEmpty(variantId, "Варіант");
        ProductId = Guard.NotEmpty(productId, "Товар");
        Name = Guard.Required(name, "Назва варіанта", NameMaxLength);
        Price = Guard.Money(price, "Ціна");
        IsActive = isActive;
        SourceVersion = sourceVersion;
        UpdatedAt = DateTime.UtcNow;
    }

    ///  Ключ задає Каталог, тому БД його не генерує. 
    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public long SourceVersion { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool ChangePrice(decimal price, long version)
    {
        if (!Accepts(version)) return false;
        Price = Guard.Money(price, "Ціна");
        Stamp(version);
        return true;
    }

    public bool Rename(string name, long version)
    {
        if (!Accepts(version)) return false;
        Name = Guard.Required(name, "Назва варіанта", NameMaxLength);
        Stamp(version);
        return true;
    }

    public bool Deactivate(long version)
    {
        if (!Accepts(version)) return false;
        IsActive = false;
        Stamp(version);
        return true;
    }

    private bool Accepts(long version) => version > SourceVersion;

    private void Stamp(long version)
    {
        SourceVersion = version;
        UpdatedAt = DateTime.UtcNow;
    }
}
