using AutoMapper;
using Catalog.Api.GrpcServices;
using Catalog.Api.Health;
using Catalog.Api.Middleware;
using Catalog.Api.Workers;
using Catalog.Bll.Caching;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using Catlog.Api.Metrics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using Orders.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<DatabaseInitializer>();

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: connectionString!,
        name: "orders-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddRedis(
    redisConnectionString: builder.Configuration.GetConnectionString("redis")!,
    name: "redis-cache",
    failureStatus: HealthStatus.Degraded,       
    tags: ["cache", "redis"],
    timeout: TimeSpan.FromSeconds(3)); ;

builder.Services.AddGrpcHealthChecks(o =>
    o.Services.MapService("", r => r.Tags.Contains("ready")));

builder.AddRedisDistributedCache("redis", configureOptions: o =>
{
    o.ConnectTimeout = 1000;
    o.SyncTimeout = 500;
    o.AsyncTimeout = 500;
});
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;
    o.TrackStatistics = true;  
});
builder.Services.AddSingleton<CacheStatistics>();
builder.Services.AddSingleton<CatalogCache>();
builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(CacheStatistics.MeterName));
builder.Services.AddHealthChecks().AddCheck<CacheHealthCheck>("cache", tags: ["cache"]);
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<CatalogProfile>());
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddSingleton<CatalogMetrics>();
builder.Services.AddHostedService<ReservationCleanupWorker>();
builder.Services.AddHostedService<CacheWarmupWorker>();

builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<GrpcLoggingInterceptor>();
    o.Interceptors.Add<DomainExceptionInterceptor>();
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();
await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
app.UseCorrelationId();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapGrpcHealthChecksService();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
}
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapHealthChecks("/health/cache", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("cache"),
        ResponseWriter = CacheHealthCheck.WriteResponse
    });

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => !check.Tags.Contains("startup")
    });
}

app.MapControllers();
app.MapGrpcService<CatalogGrpcService>();  

app.Run();
