using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Middleware;
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (DomainException ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Не знайдено"),
                ValidationException => (StatusCodes.Status400BadRequest, "Некоректні дані"),
                ConcurrencyException => (StatusCodes.Status409Conflict, "Конфлікт версій"),
                InsufficientStockException => (StatusCodes.Status409Conflict, "Недостатньо залишку"),
                ConflictException => (StatusCodes.Status409Conflict, "Конфлікт"),
                _ => (StatusCodes.Status400BadRequest, "Помилка запиту")
            };

            _logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            await WriteAsync(context, status, title, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необроблена помилка під час {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                "Внутрішня помилка сервера", "Сталася неочікувана помилка.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            },
            options: null,
            contentType: "application/problem+json");
    }
}
