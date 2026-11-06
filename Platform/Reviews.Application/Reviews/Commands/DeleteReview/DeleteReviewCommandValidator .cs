using FluentValidation;
using Reviews.Application.Common.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public sealed class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Вкажіть, хто виконує видалення.");
    }
}
