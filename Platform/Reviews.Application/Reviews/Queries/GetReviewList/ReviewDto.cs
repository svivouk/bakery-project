using Reviews.Application.Common.Logging;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;
public sealed record ReplyDto(
    [property: Sensitive] string Text,
    [property: Sensitive] string AuthorName,
    DateTime CreatedAt);
public sealed record ReviewDto(
    string Id,
    Guid ProductId,
    string ProductName,
    [property: Sensitive] string AuthorName,
    int Rating,
    [property: Sensitive] string? Text,
    string Status,
    int PhotosCount,
    ReplyDto? Reply,
    DateTime CreatedAt,
    int Version);