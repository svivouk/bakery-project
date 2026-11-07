using FluentValidation;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Bll.Validators;

namespace Orders.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services, string? mapperLicenseKey)
    {
        services.AddAutoMapper(cfg =>
        {
            if (!string.IsNullOrWhiteSpace(mapperLicenseKey))
                cfg.LicenseKey = mapperLicenseKey;
        }, typeof(OrdersMappingProfile));

        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<CreatePromoCodeRequestValidator>();
        services.AddScoped<IPromoCodeService, PromoCodeService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}

