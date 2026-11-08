using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Orders.Api.Middleware;

public sealed partial class DatabaseInitializer(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger)
{
    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var connectionString = configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("ConnectionStrings:CatalogDb не налаштовано.");

        await using var connection = new SqlConnection(connectionString);
        await OpenWithRetryAsync(connection, ct);
        await AcquireLockAsync(connection, ct);

        try
        {
            if (await ScalarAsync<int?>(connection, "SELECT CASE WHEN OBJECT_ID(N'dbo.Categories', N'U') IS NULL THEN 1 END", ct) == 1)
            {
                await RunScriptAsync(connection, "001_schema.sql", ct);
            }
            else
            {
                logger.LogInformation("Схема CatalogDb уже існує — 001_schema.sql пропущено");
            }

            await RunScriptAsync(connection, "002_procedures.sql", ct);

            var seedAllowed = environment.IsDevelopment()
                              || configuration.GetValue<bool>("Database:AllowSeedInProduction");
            var empty = await ScalarAsync<int?>(connection, "SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.Categories) THEN 1 END", ct) == 1;

            if (seedAllowed && empty)
                await RunScriptAsync(connection, "003_seed.sql", ct);
            else
                logger.LogInformation("Seed пропущено (дозволено: {Allowed}, БД порожня: {Empty})", seedAllowed, empty);
        }
        finally
        {
            await ExecAsync(connection, "EXEC sp_releaseapplock @Resource = N'CatalogDb.Init', @LockOwner = 'Session';", ct);
        }

        logger.LogInformation("Ініціалізацію CatalogDb завершено");
    }

    private async Task RunScriptAsync(SqlConnection connection, string fileName, CancellationToken ct)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Вбудований ресурс '{fileName}' не знайдено. Перевірте Build Action = Embedded resource.");

        logger.LogInformation("Виконання SQL-скрипта {Script}", fileName);

        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);

        foreach (var batch in GoSeparator().Split(sql))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await ExecAsync(connection, batch, ct);
        }
    }

    private static async Task ExecAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T?> ScalarAsync<T>(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? default : (T)value;
    }

    private static async Task AcquireLockAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @r INT;
            EXEC @r = sp_getapplock @Resource = N'CatalogDb.Init', @LockMode = 'Exclusive',
                                    @LockOwner = 'Session', @LockTimeout = 60000;
            SELECT @r;
            """;
        var result = (int)(await command.ExecuteScalarAsync(ct))!;
        if (result < 0)
            throw new InvalidOperationException($"Не вдалося отримати блокування ініціалізації CatalogDb (код {result}).");
    }

    private async Task OpenWithRetryAsync(SqlConnection connection, CancellationToken ct)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connection.OpenAsync(ct);
                return;
            }
            catch (SqlException ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "SQL Server ще не готовий (спроба {Attempt}/{Max})", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
