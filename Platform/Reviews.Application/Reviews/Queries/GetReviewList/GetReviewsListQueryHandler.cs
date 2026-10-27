using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Models;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public sealed class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    public const int MaxPageSize = 100;

    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
    {
        var criteria = new ReviewCriteria
        {
            ProductId = q.ProductId,
            CustomerId = q.AuthorId,
            Status = q.Status,
            MinRating = q.MinRating,
            MaxRating = q.MaxRating,
            Sort = q.Sort,
            IncludeDeleted = false           
        };

        return queries.GetListAsync(criteria, Math.Max(1, q.Page), Math.Clamp(q.PageSize, 1, MaxPageSize), ct);
    }
}