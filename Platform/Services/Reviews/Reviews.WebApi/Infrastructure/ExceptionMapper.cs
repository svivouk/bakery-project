using FluentValidation;
using MongoDB.Driver;
using Reviews.Application.Common.Exceptions;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.Infrastructure;

public sealed record ErrorDescriptor(
    int Status,
    string Title,
    string Detail,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    int? RetryAfterSeconds = null);
public static class ExceptionMapper
{
    public const string UnexpectedErrorDetail = "Сталася неочікувана помилка. Повідомте підтримці ідентифікатор запиту (traceId).";
    public const string DatabaseUnavailableDetail = "Сховище даних тимчасово недоступне. Спробуйте повторити запит пізніше.";
    public const int DatabaseRetryAfterSeconds = 5;

    public static ErrorDescriptor Map(Exception exception) => exception switch
    {
        // 400: запит не пройшов перевірку (ValidationBehavior). Помилки згруповано за полями.
        ValidationException v => new(StatusCodes.Status400BadRequest, "Некоректні дані",
            "Один або кілька параметрів не пройшли перевірку.", GroupErrors(v)),
        BadHttpRequestException e => new(e.StatusCode, "Некоректний HTTP-запит", "Запит не вдалося прочитати."),

        // 404
        NotFoundException e => new(StatusCodes.Status404NotFound, "Не знайдено", e.Message),

        // 409: зміна версії (оптимістична конкурентність), повторний відгук, порушення бізнес-правил
        // (ConcurrencyException і DuplicateReviewException успадковують DomainException).
        DomainException e => new(StatusCodes.Status409Conflict, "Конфлікт", e.Message),

        // 409: порушення унікального індексу, яке не перехопив репозиторій (наприклад, дубль у масовій операції).
        MongoWriteException e when e.WriteError.Category == ServerErrorCategory.DuplicateKey
            => new(StatusCodes.Status409Conflict, "Конфлікт", "Такий запис уже існує."),
        MongoBulkWriteException e when e.WriteErrors.Any(w => w.Category == ServerErrorCategory.DuplicateKey)
            => new(StatusCodes.Status409Conflict, "Конфлікт", "Такий запис уже існує."),
        MongoCommandException { Code: 11000 }
            => new(StatusCodes.Status409Conflict, "Конфлікт", "Такий запис уже існує."),

        // 503: немає з'єднання з MongoDB або не знайдено доступного сервера (драйвер тоді кидає TimeoutException
        // про вибір сервера). Клієнтові текст помилки драйвера (адреси вузлів) не віддається.
        MongoConnectionException or TimeoutException or MongoNotPrimaryException or MongoNodeIsRecoveringException
            => new(StatusCodes.Status503ServiceUnavailable, "Сервіс тимчасово недоступний", DatabaseUnavailableDetail,
                RetryAfterSeconds: DatabaseRetryAfterSeconds),

        // 500: усе інше. Текст винятку клієнтові не віддається.
        _ => new(StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера", UnexpectedErrorDetail)
    };

    private static Dictionary<string, string[]> GroupErrors(ValidationException exception) => exception.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}