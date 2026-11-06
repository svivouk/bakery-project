using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Models;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;
public sealed record GetReviewsListQuery(
    Guid? ProductId = null,
    Guid? AuthorId = null,
    ReviewStatus Status = ReviewStatus.Approved,
    int? MinRating = null,
    int? MaxRating = null,
    ReviewSort Sort = ReviewSort.Newest,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReviewDto>>, ICacheableQuery
{
    public string? GetCacheKey() =>
        Status == ReviewStatus.Approved && AuthorId is null
            ? $"reviews:list:{ProductId}:{MinRating}:{MaxRating}:{Sort}:{Page}:{Math.Clamp(PageSize, 1, GetReviewsListQueryHandler.MaxPageSize)}"
            : null;
}