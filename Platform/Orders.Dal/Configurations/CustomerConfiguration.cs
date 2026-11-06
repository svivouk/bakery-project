using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Customers;

namespace Orders.Dal.Configurations;
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", t =>
        {
            t.HasCheckConstraint("CK_Customers_Phone",
                "[AnonymizedAt] IS NOT NULL OR ([Phone] NOT LIKE '%[^0-9+]%' AND LEN([Phone]) BETWEEN 10 AND 15)");
            t.HasCheckConstraint("CK_Customers_Email", "[Email] IS NULL OR [Email] LIKE '%_@_%._%'");
            t.HasCheckConstraint("CK_Customers_Audit", ConfigurationExtensions.AuditCheckSql);
        });

        builder.ConfigureGuidKey("PK_Customers", "CustomerId");
        builder.ConfigureAudit();

        builder.Property(c => c.Name).HasMaxLength(Customer.NameMaxLength).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(Customer.PhoneMaxLength).IsUnicode(false).IsRequired(); // VARCHAR(20)
        builder.Property(c => c.Email).HasMaxLength(Customer.EmailMaxLength);                              // NULL
        builder.Property(c => c.AnonymizedAt).HasColumnType("datetime2(0)");                               // NULL

        // IsDeleted — обчислювана колонка: EF її не записує, лише читає.
        builder.Property(c => c.IsDeleted)
            .HasComputedColumnSql("CONVERT([bit], CASE WHEN [AnonymizedAt] IS NOT NULL THEN 1 ELSE 0 END)", stored: true);

        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => c.Phone).IsUnique().HasDatabaseName("UQ_Customers_Phone");
        builder.HasIndex(c => c.Email).IsUnique().HasFilter("[Email] IS NOT NULL").HasDatabaseName("UX_Customers_Email");

        // Колекція доступна лише для читання; EF заповнює її через приватне поле.
        builder.Navigation(c => c.Addresses).HasField("_addresses").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
