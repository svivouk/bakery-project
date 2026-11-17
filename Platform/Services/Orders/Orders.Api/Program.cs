using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using MongoDB.Driver.Core.Configuration;
using OpenTelemetry.Trace;
using Orders.Api;
using Orders.Api.GrpcServices;
using Orders.Api.Metrics;
using Orders.Api.Middleware;
using Orders.Bll;
using Orders.Dal;
using Orders.Dal.Seeding;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Platform.EventBus;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();   
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();
    builder.AddEventBus();

    var conectionString = builder.Configuration.GetConnectionString("OrdersDb")
        ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            connectionString: conectionString,
            name: "orders-db",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "sql", "ready"],
            timeout: TimeSpan.FromSeconds(5));
    builder.AddRabbitMQClient("messaging");
    builder.AddRabbitMQClient("messaging");
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentActor, HttpContextActor>();
    builder.Services.AddOrdersApplication(builder.Configuration["AutoMapper:LicenseKey"]);
    builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(Telemetry.SourceName));
    builder.Services.AddSingleton<OrdersMetrics>();
    builder.Services
        .AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcServerInterceptor>());
    builder.Services.AddGrpcHealthChecks(o =>
    {
        o.Services.Map("", r => r.Tags.Contains("ready"));
    });
    builder.Services.AddOrdersDb(conectionString);
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Orders API",
            Version = "v1",
            Description = "Замовлення, платежі та промокоди пекарні."
        });

        foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(Orders.Bll.Dtos.OrderDto).Assembly })
        {
            var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
            if (File.Exists(xml)) options.IncludeXmlComments(xml);
        }
    });

    var app = builder.Build();
    app.UseCorrelationId();
    var dbOptions = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    var isProduction = app.Environment.IsProduction();
    var runSeed = dbOptions.ShouldSeed(isProduction);
    app.MapGrpcService<OrdersGrpcService>();
    app.MapGrpcHealthChecksService();
    if (dbOptions.SeedOnStartup && !runSeed)
        app.Logger.LogWarning(
            "Database:SeedOnStartup ігнорується в Production: програмний сідінг пропущено. " +
            "Щоб дозволити його навмисно, задайте Database:AllowSeedInProduction=true.");

    if (dbOptions.MigrateOnStartup || runSeed)
    {
        await using var scope = app.Services.CreateAsyncScope();

        if (dbOptions.MigrateOnStartup)
            await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

        if (runSeed)
        {
            if (isProduction)
                app.Logger.LogWarning("Програмний сідінг у Production виконується, бо Database:AllowSeedInProduction=true.");

            await scope.ServiceProvider.GetRequiredService<OrdersDbSeeder>().SeedAsync();
        }
    }

    app.UseExceptionHandler();                   
    app.UseStatusCodePages();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.MapDefaultEndpoints();
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Застосунок не зміг стартувати");
}
finally
{
    Log.CloseAndFlush();
}
