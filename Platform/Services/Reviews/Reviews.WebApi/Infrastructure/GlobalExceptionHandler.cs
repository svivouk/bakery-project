using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Reviews.WebApi.Infrastructure;
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var error = ExceptionMapper.Map(exception);
        LogOnce(http, exception, error);

        var isDevelopment = environment.IsDevelopment();

        ProblemDetails problem = error.ValidationErrors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(error.ValidationErrors.ToDictionary(p => p.Key, p => p.Value));

        problem.Status = error.Status;
        problem.Title = error.Title;
        problem.Instance = http.Request.Path;
        problem.Detail = isDevelopment && error.Status >= StatusCodes.Status500InternalServerError
            ? exception.Message
            : error.Detail;

        if (isDevelopment)
        {
            problem.Extensions["exception"] = exception.GetType().FullName;
            problem.Extensions["stackTrace"] = exception.StackTrace;
        }

        http.Response.StatusCode = error.Status;
        if (error.RetryAfterSeconds is { } retryAfter)
            http.Response.Headers.RetryAfter = retryAfter.ToString();

        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception
        });

        if (!written)
        {
            await http.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
                contentType: "application/problem+json", cancellationToken: ct);
        }

        return true;
    }

    private void LogOnce(HttpContext http, Exception exception, ErrorDescriptor error)
    {
        var isServerError = error.Status >= StatusCodes.Status500InternalServerError;

        logger.Log(
            isServerError ? LogLevel.Error : LogLevel.Warning,
            isServerError ? exception : null,
            "{ExceptionType} → HTTP {Status} для {Method} {Path}: {Message}",
            exception.GetType().Name,
            error.Status,
            http.Request.Method,
            http.Request.Path,
            exception.Message);
    }
}