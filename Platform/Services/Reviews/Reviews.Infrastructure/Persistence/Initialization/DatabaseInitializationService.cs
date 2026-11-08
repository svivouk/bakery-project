using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure.Persistence.Initialization;

/// <summary>Виконується один раз під час старту застосунку, до приймання запитів: індекси, потім (за налаштуванням) сідінг.
/// Помилка тут зупиняє запуск: краще не стартувати, ніж працювати без унікальних індексів.
/// Сідінг у Production пропускається, якщо явно не задано <c>MongoDb:AllowSeedInProduction=true</c>.</summary>
public sealed class DatabaseInitializationService(
    IMongoIndexCreator indexes,
    IDataSeeder seeder,
    IOptions<MongoDbSettings> options,
    IHostEnvironment environment,
    ILogger<DatabaseInitializationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = options.Value;

        if (settings.CreateIndexesOnStartup)
            await indexes.EnsureIndexesAsync(ct);
        else
            logger.LogInformation("MongoDb:CreateIndexesOnStartup вимкнено: створення індексів пропущено.");

        if (settings.ShouldSeed(environment.IsProduction()))
        {
            if (environment.IsProduction())
                logger.LogWarning("Сідінг у Production виконується, бо MongoDb:AllowSeedInProduction=true.");

            await seeder.SeedAsync(ct);
        }
        else if (settings.SeedOnStartup)
        {
            logger.LogWarning("MongoDb:SeedOnStartup ігнорується в Production: сідінг пропущено. " +
                              "Щоб дозволити його навмисно, задайте MongoDb:AllowSeedInProduction=true.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
