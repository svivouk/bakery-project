using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Promotions;

namespace Orders.Dal.Configurations;

public sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        const int percentage = (int)DiscountType.Percentage;
        const int fixedAmount = (int)DiscountType.FixedAmount;

        builder.ToTable("PromoCodes", t =>
        {
            t.HasCheckConstraint("CK_PromoCodes_Type", $"[DiscountType] IN ({percentage}, {fixedAmount})");
            t.HasCheckConstraint("CK_PromoCodes_Value",
                $"([DiscountType] = {percentage} AND [DiscountValue] BETWEEN 0 AND 100) OR " +
                $"([DiscountType] = {fixedAmount} AND [DiscountValue] >= 0)");
            t.HasCheckConstraint("CK_PromoCodes_Period", "[ValidTo] IS NULL OR [ValidTo] > [ValidFrom]");
            t.HasCheckConstraint("CK_PromoCodes_Audit", ConfigurationExtensions.AuditCheckSql);
            t.HasCheckConstraint("CK_PromoCodes_Deleted", "[IsDeleted] = 0 OR [IsActive] = 0");
            t.HasCheckConstraint("CK_PromoCodes_MaxUses", "[MaxUses] IS NULL OR [MaxUses] > 0");
        });

        builder.ConfigureGuidKey("PK_PromoCodes", "PromoCodeId");
        builder.ConfigureAudit();

        builder.Property(p => p.Code).HasMaxLength(PromoCode.CodeMaxLength).IsRequired();
        builder.Property(p => p.DiscountType).HasColumnType("tinyint");   // 1 = відсоток, 2 = фіксована сума
        builder.Property(p => p.DiscountValue).HasColumnType("decimal(10,2)");
        builder.Property(p => p.MinOrderAmount).HasColumnType("decimal(10,2)");  // NULL
        builder.Property(p => p.ValidFrom).HasColumnType("datetime2(0)");
        builder.Property(p => p.ValidTo).HasColumnType("datetime2(0)");          // NULL
        builder.Property(p => p.MaxUses);                                        // NULL = без ліміту
        builder.Property(p => p.IsActive);
        builder.Property(p => p.IsDeleted);
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Код унікальний серед не видалених: видалений код можна використати знову.
        builder.HasIndex(p => p.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_PromoCodes_Code");
    }
}
