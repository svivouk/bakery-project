using Reviews.Domain.Common;

namespace Reviews.Domain.ValueObjects;
public sealed class Moderation : ValueObject
{
    public const int MaxReasonLength = 500;

    public DateTime ModeratedAt { get; }
    public Guid ModeratorId { get; }
    public string? Reason { get; }
    public Moderation(DateTime moderatedAt, Guid moderatorId, string? reason = null)
    {
        ModeratedAt = Guard.UtcMillis(moderatedAt, nameof(moderatedAt));
        ModeratorId = Guard.NotEmpty(moderatorId, nameof(moderatorId));
        Reason = Guard.OptionalMax(reason, nameof(reason), MaxReasonLength);
    }

    public static Moderation Approve(Guid moderatorId) =>
        new(DateTime.UtcNow, moderatorId);

    public static Moderation Reject(Guid moderatorId, string reason) =>
        new(DateTime.UtcNow, moderatorId, Guard.NotBlank(reason, nameof(reason), MaxReasonLength));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ModeratedAt;
        yield return ModeratorId;
        yield return Reason;
    }
}