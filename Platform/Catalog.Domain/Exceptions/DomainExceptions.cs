namespace Catalog.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message, Exception? inner = null) : base(message, inner) { }
}

/// Сутність не знайдена (HTTP 404).
public sealed class NotFoundException : DomainException
{
    public string Entity { get; }
    public object Key { get; }

    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' не знайдено.")
    {
        Entity = entity;
        Key = key;
    }
}

/// Некоректні вхідні дані (HTTP 400).
public sealed class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}

/// Конфлікт стану: дублікат унікального ключа, порушення обмеження (HTTP 409).
public sealed class ConflictException : DomainException
{
    public ConflictException(string message, Exception? inner = null) : base(message, inner) { }
}

/// Оптимістичний конфлікт: запис змінено кимось іншим (RowVersion не збігається) (HTTP 409).
public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string entity, object key)
        : base($"{entity} '{key}' було змінено іншим користувачем. Завантажте актуальну версію й повторіть.") { }
}

/// Недостатньо залишку для резервування (HTTP 409).
public sealed class InsufficientStockException : DomainException
{
    public Guid VariantId { get; }

    public InsufficientStockException(Guid variantId)
        : base($"Недостатньо залишку для варіанта '{variantId}'.")
    {
        VariantId = variantId;
    }
}
