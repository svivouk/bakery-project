using System.Diagnostics;
using System.Diagnostics.Metrics;
using Yarp.ReverseProxy.Forwarder;

namespace Platform.ApiGateway;

public sealed class GatewayLoggingMiddleware(RequestDelegate next, ILogger<GatewayLoggingMiddleware> logger)
{
    public static readonly Meter Meter = new("Platform.ApiGateway");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("gateway.requests");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("gateway.errors");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("gateway.request.duration", "ms");

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        var feature = context.GetReverseProxyFeature();
        var route = feature?.Route.Config.RouteId ?? "none";
        var destination = feature?.ProxiedDestination?.Model.Config.Address;
        var meta = feature?.Route.Config.Metadata;
        var team = meta is not null && meta.TryGetValue("Team", out var t) ? t : null;
        var version = meta is not null && meta.TryGetValue("Version", out var v) ? v : null;
        var status = context.Response.StatusCode;
        var tags = new KeyValuePair<string, object?>[] { new("route", route), new("status", status) };

        Requests.Add(1, tags);
        Duration.Record(sw.Elapsed.TotalMilliseconds, tags);

        logger.LogInformation(
            "{Method} {Path} від {ClientIp} -> route {Route} ({Team}/{Version}), destination {Destination}: " +
            "{StatusCode}, {Elapsed} ms, {ResponseBytes} B",
            context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress,
            route, team, version, destination, status, sw.ElapsedMilliseconds,
            context.Response.ContentLength);

        var error = context.Features.Get<IForwarderErrorFeature>();
        if (error is not null)
        {
            Errors.Add(1, tags);
            logger.LogWarning(error.Exception,
                "Сервіс недоступний: route {Route}, destination {Destination}, помилка {ForwarderError}",
                route, destination, error.Error);
        }
        else if (status >= 500)
        {
            Errors.Add(1, tags);
        }
    }
}