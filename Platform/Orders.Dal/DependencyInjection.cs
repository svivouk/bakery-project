using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Orders.Dal.Repositories;
using Orders.Dal.Repositories.Interfaces;
using Orders.Dal.Seeding;

namespace Orders.Dal;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersDb(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrdersDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<OrdersDbSeeder>();
        return services;
    }
}
public sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDERS_DB_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=orders_db;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new OrdersDbContext(options);
    }
}
