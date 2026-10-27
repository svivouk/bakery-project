using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Catalog;
using Orders.Dal.Seeding;

namespace Orders.Dal.Configurations;

public sealed class VariantRefConfiguration : IEntityTypeConfiguration<VariantRef>
{
    public void Configure(EntityTypeBuilder<VariantRef> builder)
    {
        builder.ToTable("VariantRefs", t =>
            t.HasCheckConstraint("CK_VariantRefs_Price", "[Price] >= 0"));

        builder.HasKey(v => v.Id).HasName("PK_VariantRefs");
        builder.Property(v => v.Id).HasColumnName("VariantId").ValueGeneratedNever();

        builder.Property(v => v.Name).HasMaxLength(VariantRef.NameMaxLength).IsRequired();
        builder.Property(v => v.Price).HasColumnType("decimal(10,2)");
        builder.Property(v => v.IsActive);
        builder.Property(v => v.SourceVersion);
        builder.Property(v => v.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne<ProductRef>()
            .WithMany()
            .HasForeignKey(v => v.ProductId)
            .HasConstraintName("FK_VariantRefs_Product")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(v => v.ProductId).HasDatabaseName("IX_VariantRefs_Product");
        builder.HasData(ReferenceData.Variants.Select(v => new
        {
            v.Id,
            v.ProductId,
            v.Name,
            v.Price,
            IsActive = true,
            SourceVersion = 1L,
            UpdatedAt = ReferenceData.SeededAt
        }));
    }
}
