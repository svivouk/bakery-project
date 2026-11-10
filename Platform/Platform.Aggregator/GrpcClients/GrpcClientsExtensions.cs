using Platform.Aggregator.Clients;
using Platform.Contracts.Catalog;
using Orders.Grpc;
using Reviews.Grpc;

namespace Platform.Aggregator.GrpcClients;

public static class GrpcClientsExtensions
{
    public static IServiceCollection AddAggregatorSources(this IServiceCollection services, IConfiguration configuration)
    {
        // gRPC потребує HTTP/2, тобто https-ендпоінта сервісу.
        services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(o => o.Address = new Uri("https://catalog-api"));
        services.AddGrpcClient<OrdersGrpc.OrdersGrpcClient>(o => o.Address = new Uri("https://orders-api"));
        services.AddGrpcClient<ReviewsGrpc.ReviewsGrpcClient>(o => o.Address = new Uri("https://reviews-api"));

        // HTTP-клієнти лишаються для порівняння швидкості (Лаб. 05).
        services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
        services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
        services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));

        services.AddScoped<CatalogGrpcGateway>();
        services.AddScoped<OrdersGrpcSource>();
        services.AddScoped<CatalogGrpcSource>();
        services.AddScoped<ReviewsGrpcSource>();

        if (UseHttp(configuration))
        {
            services.AddScoped<IOrdersSource>(sp => sp.GetRequiredService<OrdersClient>());
            services.AddScoped<ICatalogSource>(sp => sp.GetRequiredService<CatalogClient>());
            services.AddScoped<IReviewsSource>(sp => sp.GetRequiredService<ReviewsClient>());
        }
        else
        {
            services.AddScoped<IOrdersSource>(sp => sp.GetRequiredService<OrdersGrpcSource>());
            services.AddScoped<ICatalogSource>(sp => sp.GetRequiredService<CatalogGrpcSource>());
            services.AddScoped<IReviewsSource>(sp => sp.GetRequiredService<ReviewsGrpcSource>());
        }

        return services;
    }

    public static string TransportName(IConfiguration configuration) => UseHttp(configuration) ? "http" : "grpc";

    private static bool UseHttp(IConfiguration configuration) =>
        string.Equals(configuration["Aggregator:Transport"], "http", StringComparison.OrdinalIgnoreCase);
    public static WebApplication MapGrpcCheckEndpoints(this WebApplication app)
    {
        app.MapGet("/api/grpc-check/catalog/{slug}", async (string slug, CatalogGrpcGateway catalog, CancellationToken ct) =>
        {
            var result = await catalog.GetProductBySlugAsync(slug, ct);
            return result.Value is null
                ? Results.NotFound(new { slug, source = "catalog gRPC", cache = result.Cache })
                : Results.Ok(new
                {
                    source = "catalog gRPC",
                    cache = result.Cache,
                    product = new
                    {
                        result.Value.ProductId,
                        result.Value.Name,
                        result.Value.BasePrice,
                        variants = result.Value.Variants.Count
                    }
                });
        });
        return app;
    }
}
