using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;

namespace Orders.Dal.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Qty", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderItems_Price", "[UnitPrice] >= 0");
        });

        // Первинний ключ некластерний, а кластерний індекс (OrderId, OrderItemId) тримає позиції
        // одного замовлення поруч на диску.
        builder.ConfigureGuidKey("PK_OrderItems", "OrderItemId");
        builder.HasKey(i => i.Id).IsClustered(false);
        builder.HasIndex(i => new { i.OrderId, i.Id }).IsUnique().IsClustered().HasDatabaseName("CX_OrderItems_Order");

        builder.Property(i => i.ProductId);
        builder.Property(i => i.VariantId);

        // Знімок на момент замовлення
        builder.Property(i => i.ProductName).HasMaxLength(OrderItem.ProductNameMaxLength).IsRequired();
        builder.Property(i => i.VariantName).HasMaxLength(OrderItem.VariantNameMaxLength).IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Quantity);
        builder.Property(i => i.LineTotal)
            .HasComputedColumnSql("[UnitPrice] * [Quantity]", stored: true);

        // 1:N Order → OrderItem
        builder.HasOne<Order>()
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .HasConstraintName("FK_OrderItems_Order")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
