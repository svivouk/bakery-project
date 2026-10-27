using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Orders.Api;
using Orders.Api.Middlewear;
using Orders.Dal;
using Orders.Dal.Seeding;
using System.Reflection;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Не задано рядок підключення ConnectionStrings:OrdersDb.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpContextActor>();
builder.Services.AddOrdersDb(connectionString);                           
builder.Services.AddOrdersApplication(builder.Configuration["AutoMapper:LicenseKey"]);


builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

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
app.UseExceptionHandler();

var dbOptions = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (dbOptions.MigrateOnStartup || dbOptions.SeedOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();

    if (dbOptions.MigrateOnStartup)
    {
        // Схема + довідники, задані через HasData (модельний сідінг).
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();
    }

    if (dbOptions.SeedOnStartup)
    {
        if (app.Environment.IsProduction())
            app.Logger.LogWarning("Database:SeedOnStartup увімкнено в Production: демо-дані будуть додані. Вимкніть прапорець, якщо це не навмисно.");

        // Програмний сідер: довідники → клієнти/промокоди → кошики; кожен запис перевіряється перед вставкою.
        await scope.ServiceProvider.GetRequiredService<OrdersDbSeeder>().SeedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwagger();  
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
