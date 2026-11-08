using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence;
public sealed class RatingSummaryService(
    IProductRatingSummaryRepository summaries,
    IReviewRepository reviews) : IRatingSummaryService
{
    public Task OnApprovedAsync(Review review, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        return summaries.ApplyAsync(review.ProductId, RatingDelta.ForAdded(review.Rating), ct);
    }

    public Task OnDeletedAsync(Review review, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        return summaries.ApplyAsync(review.ProductId, RatingDelta.ForRemoved(review.Rating), ct);
    }

    public async Task ReconcileAsync(Guid productId, CancellationToken ct = default)
    {
        var stats = await reviews.AggregateRatingStatsAsync(productId, ct);
        await summaries.ReplaceAsync(productId, stats, ct);
    }
}