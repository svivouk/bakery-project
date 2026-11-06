using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Catalog;
using Orders.Dal.Seeding;

namespace Orders.Dal.Configurations;

public sealed class ProductRefConfiguration : IEntityTypeConfiguration<ProductRef>
{
    public void Configure(EntityTypeBuilder<ProductRef> builder)
    {
        builder.ToTable("ProductRefs");
        builder.HasKey(p => p.Id).HasName("PK_ProductRefs");
        builder.Property(p => p.Id).HasColumnName("ProductId").ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(ProductRef.NameMaxLength).IsRequired();
        builder.Property(p => p.IsActive);
        builder.Property(p => p.SourceVersion);   
        builder.Property(p => p.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasData(ReferenceData.Products.Select(p => new
        {
            p.Id,
            p.Name,
            IsActive = true,
            SourceVersion = 1L,
            UpdatedAt = ReferenceData.SeededAt
        }));
    }
}