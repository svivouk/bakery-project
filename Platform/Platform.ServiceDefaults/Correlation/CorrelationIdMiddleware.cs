using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace Microsoft.Extensions.Hosting;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{

    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var generated = false;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var value) || string.IsNullOrWhiteSpace(value))
        {
            value = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = value;
            generated = true;
        }

        var id = value.ToString();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;     // перезаписує заголовок від downstream
            return Task.CompletedTask;
        });
        Activity.Current?.SetTag("correlation_id", id);

        using (LogContext.PushProperty("CorrelationId", id))
        {
            if (generated)
                logger.LogInformation("Згенеровано новий CorrelationId для {Method} {Path} від {ClientIp}",
                    context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);

            logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path);
            await next(context);
            logger.LogInformation("Request finished with {StatusCode}", context.Response.StatusCode);
        }
    }
}

public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        return base.SendAsync(request, ct);
    }
}