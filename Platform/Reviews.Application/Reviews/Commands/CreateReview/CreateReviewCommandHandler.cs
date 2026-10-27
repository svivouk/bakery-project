using MediatR;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IReviewEligibilityService eligibilityService,
    IReviewEligibilityRepository eligibility,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        var review = Review.Create(
            cmd.ProductId, cmd.ProductName,
            new Author(cmd.AuthorId, cmd.AuthorName),
            cmd.OrderItemId, cmd.Rating, cmd.Text);

        var purchase = await eligibilityService.EnsureCanReviewAsync(cmd.AuthorId, cmd.OrderItemId, ct);
        if (purchase.ProductId != cmd.ProductId)
            throw new DomainException("Покупка не стосується вказаного товару.");

        var claimed = false;
        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                claimed = await eligibility.TryClaimAsync(cmd.OrderItemId, review.Id, token);
                if (!claimed) throw new DomainException("Цю покупку вже оцінено.");

                await reviews.AddAsync(review, token);     
            }, ct);
        }
        catch when (claimed)
        {
            await eligibility.ReleaseAsync(cmd.OrderItemId, review.Id, CancellationToken.None);
            throw;
        }

        return review.Id.ToString();
    }
}