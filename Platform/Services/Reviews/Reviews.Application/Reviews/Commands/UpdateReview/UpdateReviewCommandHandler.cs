using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandHandler(IReviewRepository reviews, ILogger<UpdateReviewCommandHandler> logger)
    : IRequestHandler<UpdateReviewCommand, int>
{
    public async Task<int> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        using var activity = Telemetry.Source.StartActivity("UpdateReview");
        var id = ReviewIds.Parse(cmd.ReviewId);
        var review = await reviews.GetByIdAsync(id, ct: ct)
            ?? throw new NotFoundException(nameof(Review), cmd.ReviewId);

        if (review.CustomerId != cmd.AuthorId)
            throw new DomainException("Редагувати відгук може лише його автор.");
        if (review.Version != cmd.ExpectedVersion)
            throw new ConcurrencyException(nameof(Review), cmd.ReviewId, cmd.ExpectedVersion);

        review.Edit(cmd.Rating, cmd.Text);         
        await reviews.UpdateAsync(review, ct);
        activity?.SetTag("review.id", review.Id);
        activity?.SetTag("review.productId", review.ProductId);
        activity?.SetTag("review.authorId", review.CustomerId);
        logger.LogInformation("Review {ReviewId} updated by {AuthorId}, rating {Rating}, version {Version}",
                                  review.Id, review.CustomerId, review.Rating, review.Version);
        return review.Version;
    }
}