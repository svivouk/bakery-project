using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Diagnostics;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString), "Не задано MongoDb:ConnectionString.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.DatabaseName), "Не задано MongoDb:DatabaseName.");

        MongoMappings.Register();
        services.AddSingleton<MongoCallTracker>();
        services.AddSingleton<IDatabaseCallTracker>(sp => sp.GetRequiredService<MongoCallTracker>());
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = MongoClientSettings.FromConnectionString(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString);
            settings.ApplicationName = "Reviews";
            settings.ClusterConfigurator = sp.GetRequiredService<MongoCallTracker>().Configure;
            return new MongoClient(settings);
        });

        services.AddSingleton<MongoTransactionSupport>();
        services.AddScoped<MongoDbContext>();                      
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IReviewQueries, ReviewQueries>();

        return services;
    }
}