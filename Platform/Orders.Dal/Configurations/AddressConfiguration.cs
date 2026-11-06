using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Customers;

namespace Orders.Dal.Configurations;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.ConfigureGuidKey("PK_Addresses", "AddressId");

        builder.Property(a => a.Label).HasMaxLength(Address.LabelMaxLength);          // NULL
        builder.Property(a => a.City).HasMaxLength(Address.CityMaxLength).IsRequired();
        builder.Property(a => a.Street).HasMaxLength(Address.StreetMaxLength).IsRequired();
        builder.Property(a => a.Building).HasMaxLength(Address.BuildingMaxLength).IsRequired();
        builder.Property(a => a.Apartment).HasMaxLength(Address.ApartmentMaxLength);  // NULL
        builder.Property(a => a.Comment).HasMaxLength(Address.CommentMaxLength);      // NULL
        builder.Property(a => a.IsDefault);

        // 1:N Customer → Address; адреси видаляються разом із клієнтом.
        builder.HasOne<Customer>()
            .WithMany(c => c.Addresses)
            .HasForeignKey(a => a.CustomerId)
            .HasConstraintName("FK_Addresses_Customer")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.CustomerId).HasDatabaseName("IX_Addresses_Customer");
        // одна адреса за замовчуванням на клієнта
        builder.HasIndex(a => a.CustomerId).IsUnique().HasFilter("[IsDefault] = 1").HasDatabaseName("UX_Addresses_Default");
    }
}
