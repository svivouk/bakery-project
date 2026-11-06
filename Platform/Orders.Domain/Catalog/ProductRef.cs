using Orders.Domain;

namespace Orders.Domain.Catalog;

/// Локальна копія товару з Каталогу. Змінюється лише подіями (ProductRenamed, ProductDeactivated).
/// SourceVersion — версія події: застарілі й повторні події ігноруються (методи повертають false).
public sealed class ProductRef
{
    public const int NameMaxLength = 200;

    private ProductRef() { } 

    public ProductRef(Guid productId, string name, bool isActive, long sourceVersion)
    {
        Id = Guard.NotEmpty(productId, "Товар");
        Name = Guard.Required(name, "Назва товару", NameMaxLength);
        IsActive = isActive;
        SourceVersion = sourceVersion;
        UpdatedAt = DateTime.UtcNow;
    }

    ///  Ключ задає Каталог, тому БД його не генерує. 
    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public long SourceVersion { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool Rename(string name, long version)
    {
        if (!Accepts(version)) return false;
        Name = Guard.Required(name, "Назва товару", NameMaxLength);
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
