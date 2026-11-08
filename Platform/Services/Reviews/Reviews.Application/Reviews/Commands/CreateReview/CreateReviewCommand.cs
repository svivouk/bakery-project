using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Logging;
using Reviews.Application.Reviews.Queries.GetReviewsList;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed record CreateReviewCommand(
    Guid ProductId,
    string ProductName,
    Guid AuthorId,
    [property: Sensitive] string AuthorName,
    Guid OrderItemId,
    int Rating,
    [property: Sensitive] string? Text) : ICommand<ReviewDto>;