using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Diagnostics;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();

        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;

        builder.Services.AddSerilog((services, cfg) => cfg
            .MinimumLevel.Is(builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()                                        
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter()),
            writeToProviders: true);

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
            http.AddHttpMessageHandler<CorrelationIdHandler>();
        });
        return builder;
    }
    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Environment.ApplicationName;
        var envName = builder.Environment.EnvironmentName;

        builder.Logging.AddOpenTelemetry(o => { o.IncludeFormattedMessage = true; o.IncludeScopes = true; });
        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Polly")
                .AddMeter("Orders.Api")
                .AddMeter("Catalog.Api")
                .AddMeter("Reviews.Api")
                .AddMeter("Platform.Messaging")
                .AddView("orders.operation.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[] { 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 }
                })
                .AddView("catalog.operation.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[] { 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000 }
                })
                )
            .WithTracing(t =>
            {
                t.AddSource(serviceName)                                            
                 .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")     // MongoDB
                 .AddAspNetCoreInstrumentation(o => o.Filter = ctx =>
                     !ctx.Request.Path.StartsWithSegments("/health") &&
                     !ctx.Request.Path.StartsWithSegments("/alive"))
                 .AddSource("Platform.EventBus")
                 .AddHttpClientInstrumentation()
                 .AddSqlClientInstrumentation()                                     // Dapper та EF Core
                 .AddProcessor(new ServiceTagsProcessor(serviceName, envName));

                if (builder.Environment.IsDevelopment())
                    t.SetSampler(new AlwaysOnSampler());                            // 100 %
                else
                {
                    var ratio = builder.Configuration.GetValue("Telemetry:SamplingRatio", 0.1);
                    t.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)));
                }
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();                  // адресу задає Aspire; експорт батчевий

        return builder;
    }

    internal sealed class ServiceTagsProcessor(string service, string environment) : BaseProcessor<Activity>
    {
        public override void OnStart(Activity activity)
        {
            activity.SetTag("ServiceName", service);
            activity.SetTag("Environment", environment);
        }
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<StartupState>();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck<StartupHealthCheck>("startup", tags: ["startup"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Стан запуску: сервіси без повільного старту вважаються запущеними одразу
        var startup = app.Services.GetRequiredService<StartupState>();
        if (app.Configuration["Startup:Manual"] != "true")
            app.Lifetime.ApplicationStarted.Register(startup.MarkCompleted);

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => !check.Tags.Contains("startup")
        });
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = HealthResponseWriter.WriteJsonResponse
        });
        app.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResponseWriter = HealthResponseWriter.WriteJsonResponse
        });

        return app;
    }

        
    }



