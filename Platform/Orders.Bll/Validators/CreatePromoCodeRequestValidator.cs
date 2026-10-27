using FluentValidation;
using Orders.Bll.Dtos;
using Orders.Domain.Promotions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validators;

public sealed class CreatePromoCodeRequestValidator : AbstractValidator<CreatePromoCodeRequest>
{
    public CreatePromoCodeRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.Code)
            .Must(code => ValidationRules.IsValidPromoCode(code))
            .WithMessage("Код може містити лише літери, цифри, '_' і '-', без пробілів.")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));   // порожній код уже відхилив [Required]

        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100m)
            .WithMessage("Відсоток знижки не може перевищувати 100.")
            .When(x => x.DiscountType == DiscountType.Percentage);

        RuleFor(x => x.DiscountValue)
            .Must(ValidationRules.HasMaxTwoDecimals)
            .WithMessage("Допускається щонайбільше 2 знаки після коми.");

        RuleFor(x => x.MinOrderAmount)
            .Must(amount => ValidationRules.HasMaxTwoDecimals(amount!.Value))
            .WithMessage("Допускається щонайбільше 2 знаки після коми.")
            .When(x => x.MinOrderAmount.HasValue);

        // Фіксована знижка більша за мінімальну суму замовлення зробила б замовлення безкоштовним.
        RuleFor(x => x.DiscountValue)
            .Must((x, discount) => discount <= x.MinOrderAmount!.Value)
            .WithMessage("Фіксована знижка не може перевищувати мінімальну суму замовлення.")
            .When(x => x.DiscountType == DiscountType.FixedAmount && x.MinOrderAmount.HasValue);

        RuleFor(x => x.ValidTo)
            .Must((x, validTo) => validTo > x.ValidFrom)
            .WithMessage("Кінець дії має бути пізніше за початок.")
            .When(x => x.ValidTo.HasValue);

        // Залежність від поточного часу: валідатор отримує TimeProvider через DI, тому правило легко перевірити в тесті.
        RuleFor(x => x.ValidTo)
            .Must(validTo => validTo > clock.GetUtcNow().UtcDateTime)
            .WithMessage("Термін дії вже минув.")
            .When(x => x.ValidTo.HasValue);
    }
}