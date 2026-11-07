using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Messaging;

namespace Orders.Dal.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        // Ключ — EventId з вхідної події, тому генерувати його не треба.
        builder.HasKey(m => m.EventId).HasName("PK_InboxMessages");
        builder.Property(m => m.EventId).ValueGeneratedNever();

        builder.Property(m => m.EventType).HasMaxLength(OutboxMessage.EventTypeMaxLength).IsUnicode(false).IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
