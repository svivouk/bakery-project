using FluentValidation;
using Orders.Bll.Dtos;
using Orders.Domain.Promotions;
using Orders.Domain.Specification;

namespace Orders.Bll.Validation;

public sealed class CreatePromoCodeRequestValidator : AbstractValidator<CreatePromoCodeRequest>
{
    public CreatePromoCodeRequestValidator()
    {
        RuleFor(x => x.DiscountValue)
            .HaveAtMostTwoDecimals("discountValue");

        RuleFor(x => x.DiscountValue)
            .Must(v => v <= 100m)
            .When(x => x.DiscountType == DiscountType.Percentage)
            .WithMessage("discountValue: відсоток знижки не може перевищувати 100.");

        RuleFor(x => x.MinOrderAmount)
            .Must(v => ValidationRules.HasAtMostTwoDecimals(v!.Value))
            .When(x => x.MinOrderAmount.HasValue)
            .WithMessage("minOrderAmount: допускається щонайбільше 2 знаки після коми.");

        RuleFor(x => x.ValidFrom)
            .Must(ValidationRules.HasTimeZone)
            .WithMessage("validFrom: вкажіть дату й час із часовим поясом, наприклад 2026-11-01T00:00:00Z.");

        RuleFor(x => x.ValidTo)
            .Must(v => ValidationRules.HasTimeZone(v!.Value))
            .When(x => x.ValidTo.HasValue)
            .WithMessage("validTo: вкажіть дату й час із часовим поясом, наприклад 2026-12-01T00:00:00Z.");

        RuleFor(x => x.ValidTo)
            .Must((x, to) => to!.Value.ToUniversalTime() > x.ValidFrom.ToUniversalTime())
            .When(x => x.ValidTo.HasValue && ValidationRules.HasTimeZone(x.ValidTo.Value) && ValidationRules.HasTimeZone(x.ValidFrom))
            .WithMessage("validTo: кінець дії має бути пізніше за початок.");
    }
}

public sealed class ChangeDiscountRequestValidator : AbstractValidator<ChangeDiscountRequest>
{
    public ChangeDiscountRequestValidator()
    {
        RuleFor(x => x.DiscountValue)
            .HaveAtMostTwoDecimals("discountValue");

        RuleFor(x => x.DiscountValue)
            .Must(v => v <= 100m)
            .When(x => x.DiscountType == DiscountType.Percentage)
            .WithMessage("discountValue: відсоток знижки не може перевищувати 100.");

        RuleFor(x => x.RowVersion)
            .Must(ValidationRules.IsBase64)
            .When(x => !string.IsNullOrWhiteSpace(x.RowVersion))
            .WithMessage("rowVersion: очікується рядок у форматі base64 (значення з останньої відповіді).");
    }
}

public sealed class PromoCodeListQueryValidator : PagedQueryValidator<PromoCodeListQuery>
{
    public PromoCodeListQueryValidator() : base(PromoCodeListSpecification.SortableFields) { }
}

