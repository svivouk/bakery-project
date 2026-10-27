using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Logging;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed record CreateReviewCommand(
    Guid ProductId,
    string ProductName,
    Guid AuthorId,
    [property: Sensitive] string AuthorName,
    Guid OrderItemId,
    int Rating,
    [property: Sensitive] string? Text) : ICommand<string>;