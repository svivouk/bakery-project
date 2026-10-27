using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Common.Behaviors;

namespace Reviews.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = config["LuckyPenny:LicenseKey"];

            cfg.RegisterServicesFromAssembly(assembly);       
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));       
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));  
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));  
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}