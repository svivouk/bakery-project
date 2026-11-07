using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.AuthorId).NotEmpty().WithMessage("Вкажіть автора відгуку.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Версія документа має бути додатним числом.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(Review.MinRating, Review.MaxRating)
            .WithMessage($"Оцінка має бути від {Review.MinRating} до {Review.MaxRating}.");

        RuleFor(x => x.Text)
            .MaximumLength(Review.MaxTextLength)
            .WithMessage($"Текст відгуку — не більше {Review.MaxTextLength} символів.");
    }
}