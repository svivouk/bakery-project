namespace Orders.Domain;

public class DomainException(string message) : Exception(message);
public sealed class InvalidStateException(string message) : DomainException(message);
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message, Exception? inner = null) : base(message, inner) { }
}
public sealed class UniqueConstraintException : Exception
{
    public UniqueConstraintException(string message, Exception? inner = null) : base(message, inner) { }
}

