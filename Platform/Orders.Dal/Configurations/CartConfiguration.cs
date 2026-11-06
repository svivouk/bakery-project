using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Carts;
using Orders.Domain.Customers;

namespace Orders.Dal.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts", t =>
            t.HasCheckConstraint("CK_Carts_Audit", ConfigurationExtensions.AuditCheckSql));

        builder.ConfigureGuidKey("PK_Carts", "CartId");

        builder.Property(c => c.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(c => c.RowVersion).IsRowVersion();

        // 1:1 Customer ↔ Cart: один активний кошик на клієнта
        builder.HasOne<Customer>()
            .WithOne()
            .HasForeignKey<Cart>(c => c.CustomerId)
            .HasConstraintName("FK_Carts_Customer")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.CustomerId).IsUnique().HasDatabaseName("UQ_Carts_Customer");

        builder.Navigation(c => c.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
