using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Promotions;

namespace Orders.Dal.Configurations;

public sealed class PromoCodeUsageConfiguration : IEntityTypeConfiguration<PromoCodeUsage>
{
    public void Configure(EntityTypeBuilder<PromoCodeUsage> builder)
    {
        builder.ToTable("PromoCodeUsages", t =>
            t.HasCheckConstraint("CK_PromoCodeUsages_Count", "[UsedCount] >= 0"));

        // Ключ збігається з ключем промокоду (спільний ключ): значення береться від власника, а не генерується.
        builder.HasKey(u => u.PromoCodeId).HasName("PK_PromoCodeUsages");
        builder.Property(u => u.PromoCodeId).ValueGeneratedNever();

        builder.Property(u => u.UsedCount);
        builder.Property(u => u.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        // 1:1 PromoCode ↔ PromoCodeUsage
        builder.HasOne<PromoCode>()
            .WithOne(p => p.Usage)
            .HasForeignKey<PromoCodeUsage>(u => u.PromoCodeId)
            .HasConstraintName("FK_PromoCodeUsages_Promo")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
