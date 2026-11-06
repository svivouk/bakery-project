using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public sealed record DeleteReviewCommand(string ReviewId, Guid ActorId, bool ActorIsStaff) : ICommand;