using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Carts;

namespace Orders.Dal.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", t =>
            t.HasCheckConstraint("CK_CartItems_Qty", "[Quantity] > 0"));

        builder.ConfigureGuidKey("PK_CartItems", "CartItemId");

        // ProductId/VariantId — ідентифікатори Каталогу (інший контекст), зовнішніх ключів на них немає.
        builder.Property(i => i.ProductId);
        builder.Property(i => i.VariantId);
        builder.Property(i => i.Quantity);
        builder.Property(i => i.AddedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        // 1:N Cart → CartItem
        builder.HasOne<Cart>()
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CartId)
            .HasConstraintName("FK_CartItems_Cart")
            .OnDelete(DeleteBehavior.Cascade);

        // один варіант — одна позиція в кошику
        builder.HasIndex(i => new { i.CartId, i.VariantId }).IsUnique().HasDatabaseName("UQ_CartItems_Variant");
    }
}
