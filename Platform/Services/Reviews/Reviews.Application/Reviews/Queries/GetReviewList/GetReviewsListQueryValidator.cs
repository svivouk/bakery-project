using FluentValidation;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Reviews.Queries.GetReviewList;

public sealed class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000)
            .WithMessage("Номер сторінки — від 1 до 10000.");
        RuleFor(x => x.PageSize).GreaterThan(0)
            .WithMessage("Розмір сторінки має бути додатним.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Невідомий статус відгуку.");
        RuleFor(x => x.Sort).IsInEnum().WithMessage("Невідомий порядок сортування.");

        RuleFor(x => x.ProductId).NotEqual(Guid.Empty).When(x => x.ProductId.HasValue)
            .WithMessage("ProductId не може бути порожнім ідентифікатором.");
        RuleFor(x => x.AuthorId).NotEqual(Guid.Empty).When(x => x.AuthorId.HasValue)
            .WithMessage("AuthorId не може бути порожнім ідентифікатором.");

        RuleFor(x => x.MinRating).InclusiveBetween(Review.MinRating, Review.MaxRating).When(x => x.MinRating.HasValue)
            .WithMessage($"MinRating — від {Review.MinRating} до {Review.MaxRating}.");
        RuleFor(x => x.MaxRating).InclusiveBetween(Review.MinRating, Review.MaxRating).When(x => x.MaxRating.HasValue)
            .WithMessage($"MaxRating — від {Review.MinRating} до {Review.MaxRating}.");

        RuleFor(x => x).Must(x => x.MinRating is null || x.MaxRating is null || x.MinRating <= x.MaxRating)
            .WithName("Rating").WithMessage("MinRating не може бути більшим за MaxRating.");
    }
}

