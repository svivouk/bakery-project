using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Diagnostics;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Persistence.Initialization;
using Reviews.Infrastructure.Seeding;

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
        services.AddScoped<IReviewRepository, MongoReviewRepository>();
        services.AddScoped<IReviewEligibilityRepository, MongoReviewEligibilityRepository>();
        services.AddScoped<IReviewEligibilityService, ReviewEligibilityService>();
        services.AddScoped<IProductRatingSummaryRepository, MongoProductRatingSummaryRepository>();
        services.AddScoped<IRatingSummaryService, RatingSummaryService>();

        services.AddSingleton<MongoCallTracker>();
        services.AddSingleton<IDatabaseCallTracker>(sp => sp.GetRequiredService<MongoCallTracker>());
        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var tracker = sp.GetRequiredService<MongoCallTracker>();

            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            settings.ApplicationName = "Reviews";
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(options.ServerSelectionTimeoutSeconds);
            settings.ConnectTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds);
            if (options.SocketTimeoutSeconds > 0)
                settings.SocketTimeout = TimeSpan.FromSeconds(options.SocketTimeoutSeconds);

            settings.ClusterConfigurator = cb =>
            {
                tracker.Configure(cb);                                     
                cb.Subscribe(new DiagnosticsActivityEventSubscriber());    
            };

            return new MongoClient(settings);
        });
        services.AddSingleton<IMongoIndexCreator, MongoIndexCreator>();
        services.AddSingleton<IDataSeeder, MongoDataSeeder>();
        services.AddHostedService<DatabaseInitializationService>();

        services.AddSingleton<MongoTransactionSupport>();
        services.AddScoped<MongoDbContext>();
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IReviewQueries, ReviewQueries>();

        return services;
    }
}