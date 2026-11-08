using MediatR;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Exceptions;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public sealed class DeleteReviewCommandHandler(
    IReviewRepository reviews,
    IRatingSummaryService ratingSummary,
    IUnitOfWork unitOfWork,
    ILogger<DeleteReviewCommandHandler> logger) : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        using var activity = Telemetry.Source.StartActivity("DeleteReview");
        var id = ReviewIds.Parse(cmd.ReviewId);

        var review = await reviews.GetByIdAsync(id, ct: ct)
            ?? throw new NotFoundException(nameof(Review), cmd.ReviewId);

        if (!cmd.ActorIsStaff && review.CustomerId != cmd.ActorId)
            throw new DomainException("Видалити відгук може лише його автор або працівник.");

        var wasApproved = review.Status == ReviewStatus.Approved;
        review.Delete(cmd.ActorIsStaff ? $"staff:{cmd.ActorId}" : $"customer:{cmd.ActorId}");

      
        await unitOfWork.ExecuteAsync(async token =>
        {
            await reviews.UpdateAsync(review, token);
            if (wasApproved)
                await ratingSummary.OnDeletedAsync(review, token);    
        }, ct);
        activity?.SetTag("review.id", review.Id);
        activity?.SetTag("review.productId", review.ProductId);
        activity?.SetTag("review.authorId", review.CustomerId);
        logger.LogInformation("Review {ReviewId} updated by {AuthorId}, rating {Rating}, version {Version}",
                         review.Id, review.CustomerId, review.Rating, review.Version);

    }
}