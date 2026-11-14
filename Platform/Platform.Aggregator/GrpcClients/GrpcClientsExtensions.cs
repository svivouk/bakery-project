using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Orders.Grpc;
using Platform.Aggregator.Clients;
using Platform.Contracts.Catalog;
using Polly;
using Reviews.Grpc;

namespace Platform.Aggregator.GrpcClients;

/// <summary>
/// Політики resilience клієнтів Aggregator. Вибір значень залежить від критичності сервісу для картки замовлення:
///
/// | Сервіс  | Роль                       | Повтори | Таймаут спроби | Загальний | Circuit breaker (частка / мін. запитів / пауза) |
/// |---------|----------------------------|---------|----------------|-----------|-------------------------------------------------|
/// | Orders  | критичний, перший у ланцюжку | 3     | 5 с            | 20 с      | 0.3 / 10 / 10 с                                  |
/// | Catalog | критичний                  | 5       | 4 с            | 30 с      | 0.3 / 10 / 10 с                                  |
/// | Reviews | некритичний (часткова відп.) | 1     | 2 с            | 5 с       | 0.7 / 10 / 15 с                                  |

///  * Orders: без замовлення відповіді немає взагалі, а клієнт чекає послідовно (далі йдуть інші виклики), тому повторів
///    помірно (3), а загальний час обмежено 20 с.
///  * Catalog: без товарів картку не зібрати, тому найбільше повторів (5) і найчутливіший запобіжник (0.3): краще швидко
///    визнати сервіс недоступним, ніж тримати запити.
///  * Reviews: відгуки не обов'язкові, відповідь і без них повертається (часткова, Лаб. 05). Тому швидко здаємося:
///    1 повтор, спроба 2 с, загалом 5 с. Запобіжник менш чутливий (0.7), бо поодинокі збої на відгуках не варті розмикання.
///  * Усе тут читання (ідемпотентне), тож повтори безпечні; для HTTP додатково DisableForUnsafeHttpMethods(), щоб випадковий
///    POST/PUT/DELETE ніколи не повторювався. Aggregator сьогодні нічого не пише.
///  * Обмеження: SamplingDuration ≥ 2 × AttemptTimeout, TotalRequestTimeout > AttemptTimeout (інакше валідація опцій
///    падає при старті). Retry.Delay беремо малим, щоб усі повтори вкладалися в TotalRequestTimeout.
///    
public static class GrpcClientsExtensions
{
    public static IServiceCollection AddAggregatorSources(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- gRPC (HTTP/2, тому https-ендпоінти) ----
        services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(o => o.Address = new Uri("https://catalog-api"))
            .WithGrpcRetries(maxAttempts: 5, initialBackoff: TimeSpan.FromSeconds(0.2), maxBackoff: TimeSpan.FromSeconds(2));
        services.AddGrpcClient<OrdersGrpc.OrdersGrpcClient>(o => o.Address = new Uri("https://orders-api"))
            .WithGrpcRetries(maxAttempts: 3, initialBackoff: TimeSpan.FromSeconds(0.2), maxBackoff: TimeSpan.FromSeconds(2));
        services.AddGrpcClient<ReviewsGrpc.ReviewsGrpcClient>(o => o.Address = new Uri("https://reviews-api"))
            .WithGrpcRetries(maxAttempts: 2, initialBackoff: TimeSpan.FromSeconds(0.2), maxBackoff: TimeSpan.FromSeconds(1));

#pragma warning disable EXTEXP0001
        services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler()
            .Configure((o, sp) =>
            {
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.Delay = TimeSpan.FromSeconds(0.5);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.Retry.DisableForUnsafeHttpMethods();
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                o.CircuitBreaker.FailureRatio = 0.3;
                o.CircuitBreaker.MinimumThroughput = 10;
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
                LogBreaker(o, sp, "orders-api");
            });

        services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler()
            .Configure((o, sp) =>
            {
                o.Retry.MaxRetryAttempts = 5;
                o.Retry.Delay = TimeSpan.FromSeconds(0.5);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.Retry.DisableForUnsafeHttpMethods();
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.FailureRatio = 0.3;          
                o.CircuitBreaker.MinimumThroughput = 10;
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
                LogBreaker(o, sp, "catalog-api");
            });

        services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler()
            .Configure((o, sp) =>
            {
                o.Retry.MaxRetryAttempts = 1;                  
                o.Retry.Delay = TimeSpan.FromSeconds(0.5);    
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.Retry.DisableForUnsafeHttpMethods();
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                o.CircuitBreaker.FailureRatio = 0.7;          
                o.CircuitBreaker.MinimumThroughput = 10;
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
                LogBreaker(o, sp, "reviews-api");
            });
#pragma warning restore EXTEXP0001

        services.AddScoped<CatalogGrpcGateway>();
        services.AddScoped<OrdersGrpcSource>();
        services.AddScoped<CatalogGrpcSource>();
        services.AddScoped<ReviewsGrpcSource>();

        if (UseHttp(configuration))
        {
            services.AddScoped<IOrdersSource>(sp => sp.GetRequiredService<OrdersClient>());
            services.AddScoped<ICatalogSource>(sp => sp.GetRequiredService<CatalogClient>());
            services.AddScoped<IReviewsSource>(sp => sp.GetRequiredService<ReviewsClient>());
        }
        else
        {
            services.AddScoped<IOrdersSource>(sp => sp.GetRequiredService<OrdersGrpcSource>());
            services.AddScoped<ICatalogSource>(sp => sp.GetRequiredService<CatalogGrpcSource>());
            services.AddScoped<IReviewsSource>(sp => sp.GetRequiredService<ReviewsGrpcSource>());
        }

        return services;
    }
    private static IHttpClientBuilder WithGrpcRetries(
        this IHttpClientBuilder builder, int maxAttempts, TimeSpan initialBackoff, TimeSpan maxBackoff) =>
        builder.ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = maxAttempts,
                        InitialBackoff = initialBackoff,
                        MaxBackoff = maxBackoff,
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                    }
                }
            }
        });

    private static void LogBreaker(HttpStandardResilienceOptions o, IServiceProvider sp, string service)
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger($"Resilience.{service}");

        o.CircuitBreaker.OnOpened = args =>
        {
            logger.LogError("Circuit opened for {Service} for {Duration}", service, args.BreakDuration);
            return ValueTask.CompletedTask;
        };
        o.CircuitBreaker.OnHalfOpened = _ =>
        {
            logger.LogWarning("Circuit half-open for {Service}: пробний запит", service);
            return ValueTask.CompletedTask;
        };
        o.CircuitBreaker.OnClosed = _ =>
        {
            logger.LogInformation("Circuit closed for {Service}: сервіс відновився", service);
            return ValueTask.CompletedTask;
        };
    }

    public static string TransportName(IConfiguration configuration) => UseHttp(configuration) ? "http" : "grpc";

    private static bool UseHttp(IConfiguration configuration) =>
        string.Equals(configuration["Aggregator:Transport"], "http", StringComparison.OrdinalIgnoreCase);

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