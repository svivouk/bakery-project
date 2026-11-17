using Grpc.Core;
using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Aggregator.Clients;
using Platform.Aggregator.GrpcClients;
using Platform.Aggregator.Health;
using Platform.Aggregator.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.AddRedisDistributedCache("redis", configureOptions: o =>
{
    o.ConnectTimeout = 1000;
    o.SyncTimeout = 500;
    o.AsyncTimeout = 500;
}); 

builder.Services.AddScoped<OrderCompositionService>();
builder.Services.AddScoped<CachedOrderComposition>();
builder.Services.AddAggregatorSources(builder.Configuration);

foreach (var name in new[] { "catalog-api", "orders-api", "reviews-api" })
{
    builder.Services.AddGrpcClient<Health.HealthClient>(name, o => o.Address = new Uri($"https://{name}"));
}

builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<GrpcServiceHealthCheck>("catalog-api",
        failureStatus: HealthStatus.Unhealthy, tags: ["grpc", "downstream", "ready"], args: ["catalog-api"])
    .AddTypeActivatedCheck<GrpcServiceHealthCheck>("orders-api",
        failureStatus: HealthStatus.Unhealthy, tags: ["grpc", "downstream", "ready"], args: ["orders-api"])
    .AddTypeActivatedCheck<GrpcServiceHealthCheck>("reviews-api",
        failureStatus: HealthStatus.Degraded, tags: ["grpc", "downstream"], args: ["reviews-api"]);

var app = builder.Build();
app.Logger.LogInformation("Aggregator transport: {Transport}", GrpcClientsExtensions.TransportName(app.Configuration));
app.UseCorrelationId();

app.MapGet("/api/aggregator/orders/{id:guid}/full",
    async (Guid id, CachedOrderComposition service, CancellationToken ct) =>
    {
        try
        {
            var result = await service.GetFullOrderAsync(id, ct);
            return result is null
                ? Results.Problem(statusCode: 404, title: "Замовлення не знайдено")
                : Results.Ok(result);
        }
        catch (HttpRequestException) when (!ct.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "orders-api недоступний");
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested
                                      && ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            return Results.Problem(statusCode: 503, title: "orders-api недоступний");
        }
    });

if (app.Environment.IsDevelopment())
{
    app.MapDelete("/api/aggregator/orders/{id:guid}/cache",
        async (Guid id, CachedOrderComposition service, CancellationToken ct) =>
        {
            await service.InvalidateAsync(id, "manual", ct);
            return Results.NoContent();
        });
}

app.MapDefaultEndpoints();
app.MapGrpcCheckEndpoints();
app.Run();