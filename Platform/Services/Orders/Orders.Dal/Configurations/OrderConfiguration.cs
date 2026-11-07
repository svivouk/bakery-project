using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Customers;
using Orders.Domain.Orders;
using Orders.Domain.Promotions;

namespace Orders.Dal.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Status", ConfigurationExtensions.InList<OrderStatus>("Status"));
            t.HasCheckConstraint("CK_Orders_Amounts",
                "[Subtotal] >= 0 AND [DeliveryFee] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [Subtotal]");
            t.HasCheckConstraint("CK_Orders_Audit", ConfigurationExtensions.AuditCheckSql);
        });

        builder.ConfigureGuidKey("PK_Orders", "OrderId");
        builder.ConfigureAudit();

        // Номер для клієнта береться з послідовності під час вставки.
        builder.Property(o => o.OrderNumber)
            .HasDefaultValueSql("NEXT VALUE FOR dbo.OrderNumberSeq")
            .ValueGeneratedOnAdd();

        // Enum зберігається рядком: VARCHAR(30)
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);

        builder.Property(o => o.DesiredReadyAt).HasColumnType("datetime2(0)");
        builder.Property(o => o.Comment).HasMaxLength(Order.CommentMaxLength);   // NULL

        builder.Property(o => o.Subtotal).HasColumnType("decimal(12,2)");
        builder.Property(o => o.DiscountAmount).HasColumnType("decimal(12,2)");
        builder.Property(o => o.DeliveryFee).HasColumnType("decimal(12,2)");
        builder.Property(o => o.TotalAmount)
            .HasColumnType("decimal(12,2)")
            .HasComputedColumnSql("[Subtotal] - [DiscountAmount] + [DeliveryFee]", stored: true);

        builder.Property(o => o.RowVersion).IsRowVersion();

        // Посилання на інші агрегати лише за ключем, без каскадів.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .HasConstraintName("FK_Orders_Customer")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<PromoCode>()
            .WithMany()
            .HasForeignKey(o => o.PromoCodeId)
            .HasConstraintName("FK_Orders_Promo")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(o => o.OrderNumber).IsUnique().HasDatabaseName("UQ_Orders_Number");

        builder.HasIndex(o => new { o.CustomerId, o.CreatedAt })
            .IsDescending(false, true)
            .IncludeProperties(o => new { o.OrderNumber, o.Status, o.DesiredReadyAt, o.TotalAmount })
            .HasDatabaseName("IX_Orders_Customer_Date");

        builder.HasIndex(o => new { o.Status, o.DesiredReadyAt })
            .IncludeProperties(o => new { o.OrderNumber, o.CustomerId })
            .HasDatabaseName("IX_Orders_Status_Ready");

        builder.Navigation(o => o.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
