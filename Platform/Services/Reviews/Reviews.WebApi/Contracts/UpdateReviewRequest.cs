namespace Reviews.WebApi.Contracts;

public sealed record UpdateReviewRequest(Guid AuthorId, int ExpectedVersion, int Rating, string? Text);

public sealed record UpdateReviewResponse(int Version);
