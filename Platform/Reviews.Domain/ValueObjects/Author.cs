using Reviews.Domain.Common;

namespace Reviews.Domain.ValueObjects;

public sealed class Author : ValueObject
{
    public const int MaxNameLength = 100;

    public Guid Id { get; }
    public string DisplayName { get; }

    public Author(Guid id, string displayName)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        DisplayName = Guard.NotBlank(displayName, nameof(displayName), MaxNameLength);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return DisplayName;
    }
}