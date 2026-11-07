namespace Reviews.Domain.Exceptions;
public sealed class ConcurrencyException : DomainException
{
    public string EntityName { get; }
    public object EntityId { get; }
    public int ExpectedVersion { get; }

    public ConcurrencyException(string entityName, object entityId, int expectedVersion)
        : base($"{entityName} {entityId} було змінено іншим запитом (очікувана версія {expectedVersion}). Оновіть дані й повторіть спробу.")
    {
        EntityName = entityName;
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
    }
}