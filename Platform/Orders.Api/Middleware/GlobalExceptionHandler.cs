using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Orders.Api.Middleware;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var error = ExceptionMapper.Map(exception);
        LogOnce(httpContext, exception, error);

        var isDevelopment = environment.IsDevelopment();

        ProblemDetails problem = error.ValidationErrors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(error.ValidationErrors.ToDictionary(p => p.Key, p => p.Value));

        problem.Status = error.Status;
        problem.Title = error.Title;
        problem.Instance = httpContext.Request.Path;
        problem.Detail = isDevelopment && error.Status >= StatusCodes.Status500InternalServerError
            ? exception.Message
            : error.Detail;

        if (isDevelopment)
        {
            problem.Extensions["exception"] = exception.GetType().FullName;
            problem.Extensions["stackTrace"] = exception.StackTrace;
        }

        httpContext.Response.StatusCode = error.Status;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }

        return true;
    }
    private void LogOnce(HttpContext httpContext, Exception exception, ErrorDescriptor error)
    {
        var isServerError = error.Status >= StatusCodes.Status500InternalServerError;

        logger.Log(
            isServerError ? LogLevel.Error : LogLevel.Warning,
            isServerError ? exception : null,
            "{ExceptionType} → HTTP {Status} для {Method} {Path}: {Message}",
            exception.GetType().Name,
            error.Status,
            httpContext.Request.Method,
            httpContext.Request.Path,
            exception.Message);
    }
}
