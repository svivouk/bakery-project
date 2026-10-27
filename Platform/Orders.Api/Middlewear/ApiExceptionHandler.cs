using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orders.Bll;
using Orders.Domain;

namespace Orders.Api.Middlewear
{
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
            CancellationToken cancellationToken)
        {
            var (status, title) = exception switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Не знайдено"),

                ConflictException or InvalidStateException or ConcurrencyException or UniqueConstraintException
                    => (StatusCodes.Status409Conflict, "Конфлікт"),

                DomainException => (StatusCodes.Status400BadRequest, "Некоректні дані"),
                _ => (0, string.Empty)
            };

            if (status == 0) return false;

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = exception.Message,
                    Instance = httpContext.Request.Path
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }
    }

}
