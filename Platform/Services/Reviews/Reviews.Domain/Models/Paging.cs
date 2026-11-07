using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Models;

public sealed record PageRequest
{
    public const int MaxLimit = 100;

    public int Limit { get; }
    public string? Cursor { get; }

    public PageRequest(int limit = 20, string? cursor = null)
    {
        if (limit is < 1 or > MaxLimit)
            throw new DomainException($"Розмір сторінки має бути від 1 до {MaxLimit}.");
        Limit = limit;
        Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor;
    }
}
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
