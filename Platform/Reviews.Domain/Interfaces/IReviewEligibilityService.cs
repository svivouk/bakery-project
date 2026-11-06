using Reviews.Domain.Models;

namespace Reviews.Domain.Interfaces;
public interface IReviewEligibilityService
{
    Task<ReviewEligibility> EnsureCanReviewAsync(Guid customerId, Guid orderItemId, CancellationToken ct = default);
}