using FluentValidation;
using Orders.Bll.Dtos;
using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validators;

public sealed class ChangeDiscountRequestValidator : AbstractValidator<ChangeDiscountRequest>
{
    public ChangeDiscountRequestValidator()
    {
        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100m)
            .WithMessage("Відсоток знижки не може перевищувати 100.")
            .When(x => x.DiscountType == DiscountType.Percentage);

        RuleFor(x => x.DiscountValue)
            .Must(ValidationRules.HasMaxTwoDecimals)
            .WithMessage("Допускається щонайбільше 2 знаки після коми.");

        RuleFor(x => x.RowVersion)
            .Must(value => ValidationRules.IsBase64(value))
            .WithMessage("Очікується токен версії у форматі base64 (поле rowVersion з останньої відповіді).")
            .When(x => !string.IsNullOrWhiteSpace(x.RowVersion));   // порожній токен уже відхилив [Required]
    }
}
