using FluentValidation;
using Orders.Bll.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validators;

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{    public const int MaxLeadDays = 60;

    public PlaceOrderRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.DesiredReadyAt)
            .Must(at => at > clock.GetUtcNow().UtcDateTime)
            .WithMessage("Бажаний час отримання має бути в майбутньому.");

        RuleFor(x => x.DesiredReadyAt)
            .Must(at => at <= clock.GetUtcNow().UtcDateTime.AddDays(MaxLeadDays))
            .WithMessage($"Замовити можна не далі ніж на {MaxLeadDays} днів наперед.");

        RuleFor(x => x.DeliveryFee)
            .Must(ValidationRules.HasMaxTwoDecimals)
            .WithMessage("Допускається щонайбільше 2 знаки після коми.");
        RuleFor(x => x.DeliveryFee)
            .Equal(0m)
            .WithMessage("Без адреси доставки (самовивіз) вартість доставки має бути 0.")
            .When(x => x.DeliveryAddress is null);

        RuleFor(x => x.PromoCode)
            .Must(code => ValidationRules.IsValidPromoCode(code!))
            .WithMessage("Код промокоду може містити лише літери, цифри, '_' і '-', без пробілів.")
            .When(x => !string.IsNullOrWhiteSpace(x.PromoCode));
    }
}
