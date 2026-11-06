using FluentValidation;
using Orders.Bll;
using Orders.Domain;

namespace Orders.Api.Middleware;

public sealed record ErrorDescriptor(
    int Status,
    string Title,
    string Detail,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
public static class ExceptionMapper
{
    public const string UnexpectedErrorDetail =
        "Сталася неочікувана помилка. Повідомте підтримці ідентифікатор запиту (traceId).";

    public static ErrorDescriptor Map(Exception exception) => exception switch
    {
        // 400: запит не пройшов перевірку. Помилки зібрано за полями (ValidationProblemDetails).
        ValidationException v => new(StatusCodes.Status400BadRequest, "Некоректні дані",
            "Один або кілька параметрів не пройшли перевірку.", GroupErrors(v)),
        InvalidQueryException e => new(StatusCodes.Status400BadRequest, "Некоректні дані", e.Message),
        BadHttpRequestException e => new(e.StatusCode, "Некоректний HTTP-запит", "Запит не вдалося прочитати."),

        // 404
        NotFoundException e => new(StatusCodes.Status404NotFound, "Не знайдено", e.Message),

        // 409: порушення бізнес-правил і конфлікти зі станом даних (DomainException включає InvalidStateException).
        ConflictException or DomainException or ConcurrencyException or UniqueConstraintException
            => new(StatusCodes.Status409Conflict, "Конфлікт", exception.Message),

        // 500: усе інше. Текст винятку клієнтові не віддається.
        _ => new(StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера", UnexpectedErrorDetail)
    };

    private static Dictionary<string, string[]> GroupErrors(ValidationException exception) => exception.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}
