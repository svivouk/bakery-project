using MediatR;
using Reviews.Application.Common;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandHandler(IReviewRepository reviews)
    : IRequestHandler<UpdateReviewCommand, int>
{
    public async Task<int> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var id = ReviewIds.Parse(cmd.ReviewId);
        var review = await reviews.GetByIdAsync(id, ct: ct)
            ?? throw new NotFoundException(nameof(Review), cmd.ReviewId);

        if (review.CustomerId != cmd.AuthorId)
            throw new DomainException("Редагувати відгук може лише його автор.");
        if (review.Version != cmd.ExpectedVersion)
            throw new ConcurrencyException(nameof(Review), cmd.ReviewId, cmd.ExpectedVersion);

        review.Edit(cmd.Rating, cmd.Text);         
        await reviews.UpdateAsync(review, ct);

        return review.Version;
    }
}