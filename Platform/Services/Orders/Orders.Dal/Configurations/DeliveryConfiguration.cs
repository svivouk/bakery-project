using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;

namespace Orders.Dal.Configurations;

/// <summary>Delivery — звичайна залежна сутність із власним життєвим циклом (статуси, кур'єр),
/// а не owned-тип: її можна запитувати окремо (для кур'єрської служби) без завантаження замовлення.</summary>
public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("Deliveries", t =>
        {
            t.HasCheckConstraint("CK_Deliveries_Type", ConfigurationExtensions.InList<DeliveryType>("Type"));
            t.HasCheckConstraint("CK_Deliveries_Status", ConfigurationExtensions.InList<DeliveryStatus>("Status"));
            t.HasCheckConstraint("CK_Deliveries_Address",
                "[Type] = 'Pickup' OR ([City] IS NOT NULL AND [Street] IS NOT NULL AND [Building] IS NOT NULL)");
        });

        // Спільний ключ із замовленням: OrderId одночасно PK і FK.
        builder.HasKey(d => d.OrderId).HasName("PK_Deliveries");
        builder.Property(d => d.OrderId).ValueGeneratedNever();

        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20).IsUnicode(false);    // Delivery | Pickup
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);

        builder.Property(d => d.City).HasMaxLength(Delivery.CityMaxLength);               // NULL для самовивозу
        builder.Property(d => d.Street).HasMaxLength(Delivery.StreetMaxLength);
        builder.Property(d => d.Building).HasMaxLength(Delivery.BuildingMaxLength);
        builder.Property(d => d.Apartment).HasMaxLength(Delivery.ApartmentMaxLength);
        builder.Property(d => d.AddressNote).HasMaxLength(Delivery.AddressNoteMaxLength);
        builder.Property(d => d.CourierName).HasMaxLength(Delivery.CourierNameMaxLength);
        builder.Property(d => d.CourierPhone).HasMaxLength(Delivery.CourierPhoneMaxLength).IsUnicode(false); // VARCHAR(20)
        builder.Property(d => d.DeliveredAt).HasColumnType("datetime2(0)");

        // 1:1 Order ↔ Delivery
        builder.HasOne<Order>()
            .WithOne(o => o.Delivery)
            .HasForeignKey<Delivery>(d => d.OrderId)
            .HasConstraintName("FK_Deliveries_Order")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
