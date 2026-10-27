using System.Text.RegularExpressions;
using Orders.Domain;

namespace Orders.Domain.Customers;

///Корінь агрегату «Клієнт». Замість видалення клієнта анонімізують: замовлення мають зберегтися
public sealed partial class Customer : AuditableEntity
{
    public const int NameMaxLength = 150;
    public const int PhoneMaxLength = 20;
    public const int EmailMaxLength = 256;
    public const string AnonymizedName = "Анонімний клієнт";

    private readonly List<Address> _addresses = new();

    private Customer() { } // для EF Core

    public Customer(string name, string phone, string? email = null)
    {
        Name = Guard.Required(name, "Ім'я", NameMaxLength);
        Phone = Guard.Phone(phone, "Телефон");
        Email = NormalizeEmail(email);
    }

    public string Name { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string? Email { get; private set; }
    public DateTime? AnonymizedAt { get; private set; }

    /// Обчислювана колонка БД: 1, коли заповнено AnonymizedAt (один факт — одне джерело)
    public bool IsDeleted { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<Address> Addresses => _addresses;

    public void Rename(string name)
    {
        EnsureActive();
        Name = Guard.Required(name, "Ім'я", NameMaxLength);
    }

    public void ChangePhone(string phone)
    {
        EnsureActive();
        Phone = Guard.Phone(phone, "Телефон");
    }

    public void ChangeEmail(string? email)
    {
        EnsureActive();
        Email = NormalizeEmail(email);
    }

    ///  Додає адресу. Перша адреса клієнта стає основною автоматично. 
    public Address AddAddress(string city, string street, string building,
        string? apartment = null, string? label = null, string? comment = null, bool makeDefault = false)
    {
        EnsureActive();
        var address = new Address(city, street, building, apartment, label, comment);
        if (_addresses.Count == 0 || makeDefault)
        {
            DemoteDefault();
            address.SetDefault(true);
        }
        _addresses.Add(address);
        return address;
    }

    public void UpdateAddress(Guid addressId, string city, string street, string building,
        string? apartment = null, string? label = null, string? comment = null)
    {
        EnsureActive();
        Find(addressId).Update(city, street, building, apartment, label, comment);
    }

    /// Робить адресу основною. Унікальний фільтрований індекс UX_Addresses_Default дозволяє
    /// лише одну основну, тож стару знімаємо в цій самій зміні
    public void SetDefaultAddress(Guid addressId)
    {
        EnsureActive();
        var target = Find(addressId);
        if (target.IsDefault) return;
        DemoteDefault();
        target.SetDefault(true);
    }

    public void RemoveAddress(Guid addressId)
    {
        EnsureActive();
        _addresses.Remove(Find(addressId));
    }

    ///Знеособлює персональні дані. Телефон лишається унікальним (UQ_Customers_Phone),
    /// тому замінюється випадковим значенням; адреси видаляються
    public void Anonymize()
    {
        if (AnonymizedAt is not null) return;
        Name = AnonymizedName;
        Phone = "x" + Guid.NewGuid().ToString("N")[..(PhoneMaxLength - 1)];
        Email = null;
        _addresses.Clear();
        AnonymizedAt = DateTime.UtcNow;
        IsDeleted = true;
    }

    private void DemoteDefault()
    {
        foreach (var a in _addresses.Where(a => a.IsDefault)) a.SetDefault(false);
    }

    private Address Find(Guid addressId)
        => _addresses.FirstOrDefault(a => a.Id == addressId)
           ?? throw new DomainException("Адресу клієнта не знайдено.");

    private void EnsureActive()
    {
        if (AnonymizedAt is not null)
            throw new DomainException("Клієнта анонімізовано, зміни неможливі.");
    }

    private static string? NormalizeEmail(string? email)
    {
        var v = Guard.Optional(email, "Email", EmailMaxLength);
        if (v is not null && !EmailPattern().IsMatch(v))
            throw new DomainException("Email: невірний формат.");
        return v;
    }

    // відповідає CK_Customers_Email: LIKE '%_@_%._%'
    [GeneratedRegex(@"^.+@.+\..+$")]
    private static partial Regex EmailPattern();
}
