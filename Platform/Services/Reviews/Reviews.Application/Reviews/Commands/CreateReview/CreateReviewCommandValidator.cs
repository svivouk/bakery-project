using FluentValidation;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Вкажіть товар.");
        // Асинхронне правило з запитом до MongoDB: покупку можна оцінити лише один раз. Cascade.Stop: порожній Guid
        // не доходить до бази. Перевірка раннього відхилення (зрозуміла 400-відповідь); остаточний захист від гонки —
        // унікальний індекс orderItemId і TryClaimAsync в обробнику.
        RuleFor(x => x.OrderItemId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Вкажіть покупку, яку оцінюєте.")
            .MustAsync(async (orderItemId, ct) => !await reviews.ExistsByOrderItemIdAsync(orderItemId, ct))
            .WithErrorCode("DuplicateReview")
            .WithMessage("Цю покупку вже оцінено.");
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