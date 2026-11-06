using FluentValidation;
using Orders.Bll.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validators;

public sealed class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    public RefundRequestValidator()
    {
        RuleFor(x => x.Amount)
            .Must(ValidationRules.HasMaxTwoDecimals)
            .WithMessage("Допускається щонайбільше 2 знаки після коми.");
    }
}