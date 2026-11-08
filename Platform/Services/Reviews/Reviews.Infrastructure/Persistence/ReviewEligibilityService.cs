using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence;
public sealed class ReviewEligibilityService(IReviewEligibilityRepository eligibility) : IReviewEligibilityService
{
    public async Task<ReviewEligibility> EnsureCanReviewAsync(Guid customerId, Guid orderItemId, CancellationToken ct = default)
    {
        var purchase = await eligibility.GetByOrderItemIdAsync(orderItemId, ct: ct)
            ?? throw new DomainException("Покупку не знайдено: відгук можна залишити лише на куплений товар.");

        if (purchase.CustomerId != customerId)
            throw new DomainException("Ця покупка належить іншому клієнту.");

        if (purchase.IsUsed)
            throw new DuplicateReviewException(orderItemId);

        return purchase;
    }
}