using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Logging;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed record UpdateReviewCommand(
    string ReviewId,
    Guid AuthorId,
    int ExpectedVersion,
    int Rating,
    [property: Sensitive] string? Text) : ICommand<int>;