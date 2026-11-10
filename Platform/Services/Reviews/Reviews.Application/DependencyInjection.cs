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
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });
        services.AddSingleton(new PerformanceOptions
        {
            SlowRequestThreshold = TimeSpan.FromMilliseconds(
        config.GetValue("Performance:SlowRequestThresholdMs", 500))
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}