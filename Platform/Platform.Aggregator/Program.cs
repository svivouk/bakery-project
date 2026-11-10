using System.Text.Json.Serialization;
using Grpc.Core;
using Platform.Aggregator.Clients;
using Platform.Aggregator.GrpcClients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.AddRedisDistributedCache("redis");   
builder.Services.AddScoped<OrderCompositionService>();
builder.Services.AddScoped<CachedOrderComposition>();
builder.Services.AddAggregatorSources(builder.Configuration);   

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