using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Models;

public sealed record RatingStats(int Count, int Sum, IReadOnlyDictionary<int, int> Distribution)
{
    public double Average => Count == 0 ? 0 : Math.Round((double)Sum / Count, 2);

    public static RatingStats Empty { get; } =
        new(0, 0, new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0, [5] = 0 });
}


public sealed record RatingDelta(int? Removed, int? Added)
{
    public static RatingDelta ForAdded(int rating) => new(null, Check(rating));
    public static RatingDelta ForRemoved(int rating) => new(Check(rating), null);

    private static int Check(int rating) =>
        rating is < Review.MinRating or > Review.MaxRating
            ? throw new DomainException($"Оцінка має бути від {Review.MinRating} до {Review.MaxRating}.")
            : rating;
}

public sealed record ProductRatingSummary(Guid ProductId, RatingStats Stats, long Version, DateTime UpdatedAt);