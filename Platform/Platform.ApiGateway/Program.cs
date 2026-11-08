using Platform.ApiGateway;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();   // http://orders-api -> реальна адреса від Aspire

builder.Services.AddOpenTelemetry().WithMetrics(m => m
    .AddMeter(GatewayLoggingMiddleware.Meter.Name)   // власні метрики шлюзу
    .AddMeter("Yarp.ReverseProxy"));
var app = builder.Build();

app.UseCorrelationId();
app.UseMiddleware<GatewayLoggingMiddleware>();       
app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
