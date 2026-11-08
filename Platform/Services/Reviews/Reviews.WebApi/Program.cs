using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using MongoDB.Driver;
using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.Infrastructure.Persistence;
using Reviews.WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// MediatR + конвеєр (логування, валідація, продуктивність) + валідатори FluentValidation
builder.Services.AddApplication(builder.Configuration);
// MongoDB: контекст, транзакції, запис (MongoReviewRepository) і читання (ReviewQueries)
builder.Services.AddInfrastructure(builder.Configuration);

// Aspire передає рядок підключення як ConnectionStrings:ReviewsDb (база береться з нього).
builder.Services.PostConfigure<MongoDbSettings>(o =>
{
    var cs = builder.Configuration.GetConnectionString("ReviewsDb");
    if (string.IsNullOrWhiteSpace(cs)) return;

    o.ConnectionString = cs;
    var dbName = MongoUrl.Create(cs).DatabaseName;
    if (!string.IsNullOrWhiteSpace(dbName)) o.DatabaseName = dbName;
});

// AutoMapper: профіль сутність → DTO (Application) і HTTP-контракт → команда (WebApi); один реєстр на обидві збірки.
builder.Services.AddAutoMapper(
    cfg => cfg.LicenseKey = builder.Configuration["LuckyPenny:LicenseKey"],
    typeof(Reviews.Application.DependencyInjection).Assembly,
    typeof(Program).Assembly);

builder.Services
    .AddControllers()
    // enum передаються рядками ("Pending", "Newest"), а не числами
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Єдиний формат помилок (ProblemDetails): винятки обробляє GlobalExceptionHandler, порожні 404/405 — UseStatusCodePages.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Reviews API",
        Version = "v1",
        Description = "Відгуки на товари: створення, редагування з оптимістичною конкурентністю, перегляд і видалення."
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseCorrelationId();     
app.UseExceptionHandler();  
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();      
}

app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();