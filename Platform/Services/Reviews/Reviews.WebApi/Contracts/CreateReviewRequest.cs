namespace Reviews.WebApi.Contracts;

public sealed record CreateReviewRequest(
    Guid ProductId,
    string ProductName,
    Guid AuthorId,
    string AuthorName,
    Guid OrderItemId,
    int Rating,
    string? Text);
