using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Domain.Carts;
using Orders.Domain.Catalog;
using Orders.Domain.Customers;
using Orders.Domain.Promotions;
using System.Data;


namespace Orders.Dal.Seeding;

public sealed class OrdersDbSeeder
{
    private const string LockSql =
        "EXEC @result = sp_getapplock @Resource = N'OrdersDb.Seeder', @LockMode = N'Exclusive', " +
        "@LockOwner = N'Transaction', @LockTimeout = 30000;";

    private readonly OrdersDbContext _db;
    private readonly ILogger<OrdersDbSeeder> _logger;

    public OrdersDbSeeder(OrdersDbContext db, ILogger<OrdersDbSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLockAsync(cancellationToken);

        var references = await SeedReferenceDataAsync(cancellationToken);   // 1. довідники
        var independent = await SeedCustomersAndPromoCodesAsync(cancellationToken); // 2. самостійні агрегати
        var dependent = await SeedCartsAsync(cancellationToken);            // 3. залежні від клієнта

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Сідінг завершено. Додано: довідників {References}, клієнтів/промокодів {Independent}, кошиків {Carts}. Нуль означає, що дані вже були.",
            references, independent, dependent);
    }

    private async Task<int> SeedReferenceDataAsync(CancellationToken ct)
    {
        var productIds = ReferenceData.Products.Select(p => p.Id).ToList();
        var existingProducts = (await _db.ProductRefs.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var p in ReferenceData.Products.Where(p => !existingProducts.Contains(p.Id)))
            _db.ProductRefs.Add(new ProductRef(p.Id, p.Name, isActive: true, sourceVersion: 1));
        var added = await _db.SaveChangesAsync(ct);

        var variantIds = ReferenceData.Variants.Select(v => v.Id).ToList();
        var existingVariants = (await _db.VariantRefs.AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var v in ReferenceData.Variants.Where(v => !existingVariants.Contains(v.Id)))
            _db.VariantRefs.Add(new VariantRef(v.Id, v.ProductId, v.Name, v.Price, isActive: true, sourceVersion: 1));

        return added + await _db.SaveChangesAsync(ct);
    }

    private async Task<int> SeedCustomersAndPromoCodesAsync(CancellationToken ct)
    {
        foreach (var seed in DemoData.PromoCodes)
        {
            var code = PromoCode.NormalizeCode(seed.Code);
            if (await _db.PromoCodes.AnyAsync(p => p.Code == code && !p.IsDeleted, ct)) continue;

            _db.PromoCodes.Add(new PromoCode(
                seed.Code, seed.Type, seed.Value,
                validFrom: ReferenceData.SeededAt,
                validTo: null,
                minOrderAmount: seed.MinOrderAmount,
                maxUses: seed.MaxUses));
        }

        if (!await _db.Customers.AnyAsync(c => c.Phone == DemoData.CustomerPhone, ct))
        {
            var customer = new Customer(DemoData.CustomerName, DemoData.CustomerPhone);
            customer.AddAddress("Київ", "Хрещатик", "1", apartment: "10", label: "Дім");
            _db.Entry(customer).Property(c => c.Id).CurrentValue = DemoData.CustomerId;
            _db.Customers.Add(customer);
        }

        return await _db.SaveChangesAsync(ct);
    }

    private async Task<int> SeedCartsAsync(CancellationToken ct)
    {
        var customerId = await _db.Customers
            .Where(c => c.Phone == DemoData.CustomerPhone)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
        if (customerId == Guid.Empty) return 0;

        if (await _db.Carts.AnyAsync(c => c.CustomerId == customerId || c.Id == DemoData.CartId, ct)) return 0;

        var cart = new Cart(customerId);
        cart.AddItem(DemoData.CartProductId, DemoData.CartVariantId, DemoData.CartQuantity);
        _db.Entry(cart).Property(c => c.Id).CurrentValue = DemoData.CartId;
        _db.Carts.Add(cart);

        return await _db.SaveChangesAsync(ct);
    }

    private async Task AcquireLockAsync(CancellationToken ct)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };

        await _db.Database.ExecuteSqlRawAsync(LockSql, new object[] { result }, ct);
        if (result.Value is not int code || code < 0)
            throw new InvalidOperationException(
                $"Не вдалося отримати замок сідінгу (sp_getapplock повернув {result.Value}). Інший екземпляр сідить надто довго.");
    }
}
