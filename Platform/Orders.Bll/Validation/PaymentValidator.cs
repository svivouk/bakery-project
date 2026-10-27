using FluentValidation;
using Orders.Bll.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validation;

public sealed class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    public RefundRequestValidator()
    {
        RuleFor(x => x.Amount)
            .HaveAtMostTwoDecimals("amount");
    }
}
