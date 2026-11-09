using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OpenTelemetry.Trace;
using Orders.Api;
using Orders.Api.Middleware;
using Orders.Bll;
using Orders.Dal;
using Orders.Dal.Seeding;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();   // ловить помилки старту
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();

    var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
        ?? throw new InvalidOperationException("Не задано рядок підключення ConnectionStrings:OrdersDb.");
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentActor, HttpContextActor>();
    builder.Services.AddOrdersDb(connectionString);                                          // DbContext + Unit of Work + сідер
    builder.Services.AddOrdersApplication(builder.Configuration["AutoMapper:LicenseKey"]);   // BLL, валідатори, AutoMapper
    builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(Telemetry.SourceName));
    builder.Services
        .AddControllers()
        // enum передаються рядками ("Percentage"), а не числами
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Єдиний формат помилок (ProblemDetails): винятки обробляє GlobalExceptionHandler, порожні 404/405 — UseStatusCodePages.
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
    builder.Services.AddEndpointsApiExplorer();
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
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); // ваша назва контексту
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            log.LogInformation("Застосування міграцій OrdersDb");
            await db.Database.MigrateAsync();
        }
    }
    app.UseCorrelationId();
    // Ініціалізація БД. Міграції: схема + довідники з HasData. Програмний сідер: лише демо-дані, і в Production
    // він пропускається, якщо явно не задано Database:AllowSeedInProduction=true.
    var dbOptions = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    var isProduction = app.Environment.IsProduction();
    var runSeed = dbOptions.ShouldSeed(isProduction);

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

    app.UseExceptionHandler();                   // невиловлені винятки → GlobalExceptionHandler
    app.UseStatusCodePages();                    // порожні 4xx/5xx (невідомий маршрут, 405) → ProblemDetails

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();   
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
