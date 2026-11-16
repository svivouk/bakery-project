namespace Reviews.WebApi;

public static class Chaos
{
    private static volatile int _delayMs;
    private static volatile int _failPercent;

    public static WebApplication UseChaos(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return app;

        // POST /test/chaos?delayMs=3000&failPercent=0
        app.MapPost("/test/chaos", (int? delayMs, int? failPercent) =>
        {
            _delayMs = delayMs ?? 0;
            _failPercent = failPercent ?? 0;
            return Results.Ok(new { _delayMs, _failPercent });
        });

        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (path.StartsWithSegments("/test") || path.StartsWithSegments("/health") || path.StartsWithSegments("/alive"))
            {
                await next();
                return;
            }

            if (_delayMs > 0) await Task.Delay(_delayMs, ctx.RequestAborted);
            if (_failPercent > 0 && Random.Shared.Next(100) < _failPercent)
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
            await next();
        });

        return app;
    }
}