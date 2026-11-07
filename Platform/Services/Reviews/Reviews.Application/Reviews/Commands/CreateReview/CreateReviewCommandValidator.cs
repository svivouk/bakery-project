using FluentValidation;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Вкажіть товар.");
        RuleFor(x => x.OrderItemId).NotEmpty().WithMessage("Вкажіть покупку, яку оцінюєте.");
        RuleFor(x => x.AuthorId).NotEmpty().WithMessage("Вкажіть автора відгуку.");

        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Назва товару обов'язкова.")
            .MaximumLength(Review.MaxProductNameLength)
            .WithMessage($"Назва товару — не більше {Review.MaxProductNameLength} символів.");

        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Ім'я автора обов'язкове.")
            .MaximumLength(Author.MaxNameLength)
            .WithMessage($"Ім'я автора — не більше {Author.MaxNameLength} символів.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(Review.MinRating, Review.MaxRating)
            .WithMessage($"Оцінка має бути від {Review.MinRating} до {Review.MaxRating}.");

        RuleFor(x => x.Text)
            .MaximumLength(Review.MaxTextLength)
            .WithMessage($"Текст відгуку — не більше {Review.MaxTextLength} символів.");
    }
}
