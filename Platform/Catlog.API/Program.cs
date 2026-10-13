using Catalog.Api.Middleware;
using Catalog.Api.Workers;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using AutoMapper;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<CatalogProfile>());
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStockService, StockService>();

builder.Services.AddHostedService<ReservationCleanupWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
}

app.MapControllers();

app.Run();
