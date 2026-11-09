using Platform.Contracts.Catalog;

namespace Platform.Aggregator.GrpcClients;

public static class GrpcClientsExtensions
{
    public static IServiceCollection AddMicroserviceGrpcClients(this IServiceCollection services)
    {
        services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(o => o.Address = new Uri("https://catalog-api"));
        services.AddScoped<CatalogGrpcGateway>();
        return services;
    }
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
