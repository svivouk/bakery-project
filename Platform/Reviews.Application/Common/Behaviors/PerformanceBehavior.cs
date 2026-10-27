using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Common.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
    IDatabaseCallTracker tracker,
    PerformanceOptions options)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        using var db = tracker.BeginScope();
        var started = Stopwatch.GetTimestamp();

        try
        {
            return await next();
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var name = typeof(TRequest).Name;

            if (elapsed > options.SlowRequestThreshold)
            {
                logger.LogWarning(
                    "Long running request {RequestName}: {ElapsedMs} ms (поріг {ThresholdMs} ms). " +
                    "MongoDB: {DbCommandCount} команд за {DbTotalMs} ms, найповільніша {SlowestCommand} {SlowestMs} ms",
                    name, (long)elapsed.TotalMilliseconds, (long)options.SlowRequestThreshold.TotalMilliseconds,
                    db.CommandCount, (long)db.TotalDuration.TotalMilliseconds,
                    db.SlowestCommand, (long)db.SlowestDuration.TotalMilliseconds);
            }
            else
            {
                logger.LogDebug("{RequestName} виконано за {ElapsedMs} ms; MongoDB: {DbCommandCount} команд за {DbTotalMs} ms",
                    name, (long)elapsed.TotalMilliseconds, db.CommandCount, (long)db.TotalDuration.TotalMilliseconds);
            }
        }
    }
}