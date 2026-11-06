using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;

namespace Orders.Dal;
internal static class ConfigurationExtensions
{
    public const string AuditCheckSql = "[UpdatedAt] >= [CreatedAt]";
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(128).HasDefaultValueSql("SUSER_SNAME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedBy).HasMaxLength(128).HasDefaultValueSql("SUSER_SNAME()");
    }
    public static void ConfigureGuidKey<T>(this EntityTypeBuilder<T> builder, string keyName, string columnName)
        where T : Entity
    {
        builder.HasKey(e => e.Id).HasName(keyName);
        builder.Property(e => e.Id).HasColumnName(columnName).HasDefaultValueSql("NEWSEQUENTIALID()");
    }
    public static string InList<TEnum>(string column) where TEnum : struct, Enum
        => $"[{column}] IN ({string.Join(",", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
