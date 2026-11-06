using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Application.Reviews.Queries.GetReviewById;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Reviews.Queries;

public sealed class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
