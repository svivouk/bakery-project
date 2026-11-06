using Serilog.Context;

namespace Orders.Api.Middleware;

public sealed class LogContextMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var h) && !string.IsNullOrWhiteSpace(h)
            ? h.ToString()
            : context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        var userId = context.User.FindFirst("sub")?.Value
            ?? context.User.Identity?.Name
            ?? "anonymous";

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        {
            await next(context);
        }
    }
}
