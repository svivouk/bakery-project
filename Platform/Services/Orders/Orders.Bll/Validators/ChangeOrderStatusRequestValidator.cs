using FluentValidation;
using Orders.Bll.Dtos;
using Orders.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validators;

public sealed class ChangeOrderStatusRequestValidator : AbstractValidator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEqual(OrderStatus.New)
            .WithMessage("Замовлення не можна повернути в статус New.");
    }
}