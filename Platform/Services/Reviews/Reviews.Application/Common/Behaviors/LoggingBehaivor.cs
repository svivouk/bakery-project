using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Exceptions;
using System.Diagnostics;

namespace Reviews.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private static readonly string Kind = DetectKind();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestName"] = name,
            ["RequestKind"] = Kind
        });

        logger.LogInformation("Handling {RequestKind} {RequestName} {@Request}", Kind, name, request);

        var started = Stopwatch.GetTimestamp();
        var response = await next();
        var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        logger.LogInformation("Handled {RequestKind} {RequestName} in {ElapsedMs} ms", Kind, name, elapsedMs);
        logger.LogDebug("Response of {RequestName}: {@Response}", name, response);

        return response;
    }

    private static string DetectKind()
    {
        var interfaces = typeof(TRequest).GetInterfaces();
        if (interfaces.Any(i => i == typeof(ICommand) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))))
            return "Command";
        if (interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            return "Query";
        return "Request";
    }
}
