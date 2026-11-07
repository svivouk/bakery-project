using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Messaging;

namespace Orders.Dal.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", t =>
            t.HasCheckConstraint("CK_OutboxMessages_Json", "ISJSON([Payload]) = 1"));

        builder.HasKey(m => m.Id).HasName("PK_OutboxMessages");
        builder.Property(m => m.Id).HasColumnName("OutboxId").UseIdentityColumn();

        builder.Property(m => m.EventId).HasDefaultValueSql("NEWID()");
        builder.Property(m => m.EventType).HasMaxLength(OutboxMessage.EventTypeMaxLength).IsUnicode(false).IsRequired();
        builder.Property(m => m.AggregateId);
        builder.Property(m => m.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(m => m.ProcessedAt).HasColumnType("datetime2(3)");   // NULL, поки не відправлено

        builder.HasIndex(m => m.EventId).IsUnique().HasDatabaseName("UQ_OutboxMessages_Event");

        // Фонова служба вибирає лише невідправлені: індекс містить тільки такі рядки.
        builder.HasIndex(m => m.Id).HasFilter("[ProcessedAt] IS NULL").HasDatabaseName("IX_Outbox_Unprocessed");
    }
}
