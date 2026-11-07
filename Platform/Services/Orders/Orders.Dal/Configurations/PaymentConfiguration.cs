using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;
using Orders.Domain.Payments;

namespace Orders.Dal.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Method", ConfigurationExtensions.InList<PaymentMethod>("Method"));
            t.HasCheckConstraint("CK_Payments_Status", ConfigurationExtensions.InList<PaymentStatus>("Status"));
            t.HasCheckConstraint("CK_Payments_Amount",
                "[Amount] > 0 AND [RefundedAmount] >= 0 AND [RefundedAmount] <= [Amount]");
            // узгодженість статусу з датою оплати й сумою повернення
            t.HasCheckConstraint("CK_Payments_State",
                "([Status] IN ('Pending','Failed') AND [PaidAt] IS NULL AND [RefundedAmount] = 0) OR " +
                "([Status] = 'Paid' AND [PaidAt] IS NOT NULL AND [RefundedAmount] = 0) OR " +
                "([Status] = 'PartiallyRefunded' AND [PaidAt] IS NOT NULL AND [RefundedAmount] > 0 AND [RefundedAmount] < [Amount]) OR " +
                "([Status] = 'Refunded' AND [PaidAt] IS NOT NULL AND [RefundedAmount] = [Amount])");
            t.HasCheckConstraint("CK_Payments_Audit", ConfigurationExtensions.AuditCheckSql);
        });

        builder.ConfigureGuidKey("PK_Payments", "PaymentId");
        builder.ConfigureAudit();

        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(p => p.Amount).HasColumnType("decimal(12,2)");
        builder.Property(p => p.RefundedAmount).HasColumnType("decimal(12,2)");
        builder.Property(p => p.Currency).HasColumnType("char(3)").IsUnicode(false).IsFixedLength();
        builder.Property(p => p.ExternalTransactionId).HasMaxLength(Payment.ExternalTransactionIdMaxLength).IsUnicode(false); // NULL
        builder.Property(p => p.PaidAt).HasColumnType("datetime2(0)");                                                          // NULL
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Платіж — окремий агрегат: зв'язок із замовленням лише за ключем, без навігації й каскаду.
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(p => p.OrderId)
            .HasConstraintName("FK_Payments_Order")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(p => new { p.OrderId, p.CreatedAt })
            .IsDescending(false, true)
            .IncludeProperties(p => new { p.Method, p.Status })
            .HasDatabaseName("IX_Payments_Order");

        builder.HasIndex(p => p.ExternalTransactionId)
            .IsUnique()
            .HasFilter("[ExternalTransactionId] IS NOT NULL")
            .HasDatabaseName("UX_Payments_External");

        // на замовлення може бути лише один успішний платіж
        builder.HasIndex(p => p.OrderId)
            .IsUnique()
            .HasFilter("[Status] = 'Paid'")
            .HasDatabaseName("UX_Payments_OnePaid");

        // RefundableAmount — обчислюється в коді, у БД колонки немає
        builder.Ignore(p => p.RefundableAmount);
    }
}
