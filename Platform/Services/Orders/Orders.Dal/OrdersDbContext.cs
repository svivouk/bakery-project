using Microsoft.EntityFrameworkCore;
using Orders.Domain.Carts;
using Orders.Domain.Catalog;
using Orders.Domain;
using Orders.Domain.Customers;
using Orders.Domain.Messaging;
using Orders.Domain.Payments;
using Orders.Domain.Promotions;
using Orders.Domain.Orders;

namespace Orders.Dal;
public interface ICurrentActor
{
    string Name { get; }
}

public sealed class SystemActor : ICurrentActor
{
    public static readonly SystemActor Instance = new();
    public string Name => "system:orders";
}

public sealed class OrdersDbContext : DbContext
{
    private readonly ICurrentActor _actor;

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options, ICurrentActor? actor = null)
        : base(options)
    {
        _actor = actor ?? SystemActor.Instance;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ProductRef> ProductRefs => Set<ProductRef>();
    public DbSet<VariantRef> VariantRefs => Set<VariantRef>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("OrderNumberSeq", "dbo").StartsAt(100001).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    private void ApplyAudit()
    {
        var now = DateTime.UtcNow;
        var actor = _actor.Name;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = actor;
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    entry.Property(e => e.UpdatedBy).CurrentValue = actor;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    entry.Property(e => e.UpdatedBy).CurrentValue = actor;
                    break;
            }
        }
    }
}
