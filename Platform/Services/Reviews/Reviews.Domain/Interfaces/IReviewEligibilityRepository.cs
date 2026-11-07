using MongoDB.Bson;
using Reviews.Domain.Models;

namespace Reviews.Domain.Interfaces;

public interface IReviewEligibilityRepository
{
    Task<ReviewEligibility?> GetByOrderItemIdAsync(Guid orderItemId, ReadConsistency consistency = ReadConsistency.Strong,
                                                   CancellationToken ct = default);
    Task<IReadOnlyList<ReviewEligibility>> ListAvailableAsync(Guid customerId, CancellationToken ct = default);
    Task UpsertAsync(ReviewEligibility eligibility, CancellationToken ct = default);
    Task<bool> TryClaimAsync(Guid orderItemId, ObjectId reviewId, CancellationToken ct = default);
    Task ReleaseAsync(Guid orderItemId, ObjectId reviewId, CancellationToken ct = default);
}