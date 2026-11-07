using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Queries.GetReviewsList;

namespace Reviews.Application.Reviews.Queries.GetReviewById;

public sealed record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;