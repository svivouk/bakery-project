using Orders.Domain;

namespace Orders.Domain.Customers;

///Адреса доставки клієнта. Змінюється лише через <see cref="Customer"/>.
public sealed class Address : Entity
{
    public const int LabelMaxLength = 50;
    public const int CityMaxLength = 100;
    public const int StreetMaxLength = 150;
    public const int BuildingMaxLength = 20;
    public const int ApartmentMaxLength = 20;
    public const int CommentMaxLength = 200;

    private Address() { } // для EF Core

    internal Address(string city, string street, string building, string? apartment, string? label, string? comment)
    {
        Update(city, street, building, apartment, label, comment);
    }

    public Guid CustomerId { get; private set; }
    public string? Label { get; private set; }
    public string City { get; private set; } = null!;
    public string Street { get; private set; } = null!;
    public string Building { get; private set; } = null!;
    public string? Apartment { get; private set; }
    public string? Comment { get; private set; }
    public bool IsDefault { get; private set; }

    internal void Update(string city, string street, string building, string? apartment, string? label, string? comment)
    {
        City = Guard.Required(city, "Місто", CityMaxLength);
        Street = Guard.Required(street, "Вулиця", StreetMaxLength);
        Building = Guard.Required(building, "Будинок", BuildingMaxLength);
        Apartment = Guard.Optional(apartment, "Квартира", ApartmentMaxLength);
        Label = Guard.Optional(label, "Назва адреси", LabelMaxLength);
        Comment = Guard.Optional(comment, "Коментар", CommentMaxLength);
    }

    internal void SetDefault(bool value) => IsDefault = value;
}
