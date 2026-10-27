using FluentValidation;
using Orders.Bll.Dtos;
using Orders.Domain.Specification;


namespace Orders.Bll.Validation;

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.DesiredReadyAt)
            .Must(ValidationRules.HasTimeZone)
            .WithMessage("desiredReadyAt: вкажіть дату й час із часовим поясом, наприклад 2026-11-01T10:00:00Z.");

        // Поточний час береться з TimeProvider, тож правило перевіряється в тестах без очікування.
        RuleFor(x => x.DesiredReadyAt)
            .Must(d => d.ToUniversalTime() > clock.GetUtcNow().UtcDateTime)
            .When(x => ValidationRules.HasTimeZone(x.DesiredReadyAt))
            .WithMessage("desiredReadyAt: бажаний час отримання має бути в майбутньому.");

        RuleFor(x => x.DeliveryFee)
            .HaveAtMostTwoDecimals("deliveryFee");

        RuleFor(x => x.DeliveryFee)
            .Must(fee => fee == 0m)
            .When(x => x.DeliveryAddress is null)
            .WithMessage("deliveryFee: для самовивозу вартість доставки має бути 0 (адресу не вказано).");
    }
}

public sealed class OrderListQueryValidator : PagedQueryValidator<OrderListQuery>
{
    public OrderListQueryValidator() : base(OrderListSpecification.SortableFields)
    {
        RuleFor(q => q.CreatedTo)
            .Must((q, to) => q.CreatedFrom is not { } from || from <= to!.Value)
            .When(q => q.CreatedTo.HasValue)
            .WithMessage("createdFrom не може бути пізніше за createdTo.");

        RuleFor(q => q.MaxTotal)
            .Must((q, max) => q.MinTotal is not { } min || min <= max!.Value)
            .When(q => q.MaxTotal.HasValue)
            .WithMessage("minTotal не може бути більшим за maxTotal.");
    }
}
