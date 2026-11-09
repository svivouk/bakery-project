using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.GrpcClients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));
builder.AddRedisDistributedCache("redis");   
builder.Services.AddScoped<CatalogClient>();  
builder.Services.AddScoped<OrderCompositionService>();
builder.Services.AddScoped<CachedOrderComposition>();
builder.Services.AddMicroserviceGrpcClients();

var app = builder.Build();
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
    });

if (app.Environment.IsDevelopment())
{    app.MapDelete("/api/aggregator/orders/{id:guid}/cache",
        async (Guid id, CachedOrderComposition service, CancellationToken ct) =>
        {
            await service.InvalidateAsync(id, "manual", ct);
            return Results.NoContent();
        });
}

app.MapDefaultEndpoints();
app.MapGrpcCheckEndpoints();
app.Run();