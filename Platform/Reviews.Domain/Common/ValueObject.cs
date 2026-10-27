namespace Reviews.Domain.Common;
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is not null && obj.GetType() == GetType() &&
        GetEqualityComponents().SequenceEqual(((ValueObject)obj).GetEqualityComponents());

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (h, c) => HashCode.Combine(h, c));

    public static bool operator ==(ValueObject? a, ValueObject? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);
}