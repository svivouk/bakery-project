namespace Reviews.Infrastructure.Persistence;
public enum TransactionMode
{
    Auto,
    Enabled,
    Disabled
}

/// <summary>Секція "MongoDb" конфігурації. Значення різняться за середовищами (appsettings.{Environment}.json);
/// будь-яке можна перевизначити змінною середовища, наприклад <c>MongoDb__ConnectionString</c>.</summary>
public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "reviews_db";
    public TransactionMode Transactions { get; set; } = TransactionMode.Auto;

    // ---- таймаути драйвера (секунди) ----

    /// <summary>Скільки драйвер шукає доступний сервер, перш ніж кинути TimeoutException (у API це 503).</summary>
    public int ServerSelectionTimeoutSeconds { get; set; } = 30;

    /// <summary>Таймаут встановлення TCP-з'єднання з вузлом.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>Таймаут читання/запису в сокеті. 0: без обмеження (значення драйвера за замовчуванням).</summary>
    public int SocketTimeoutSeconds { get; set; }

    // ---- ініціалізація під час старту ----

    /// <summary>Створити індекси (compound, text, unique, partial, TTL) через <c>Indexes.CreateManyAsync</c>. Операція ідемпотентна.</summary>
    public bool CreateIndexesOnStartup { get; set; } = true;

    /// <summary>Заповнити порожні колекції демонстраційними даними (<see cref="Seeding.IDataSeeder"/>).</summary>
    public bool SeedOnStartup { get; set; }

    /// <summary>Явний дозвіл на сідінг у Production. Без нього <see cref="SeedOnStartup"/> там ігнорується.</summary>
    public bool AllowSeedInProduction { get; set; }

    public bool ShouldSeed(bool isProduction) => SeedOnStartup && (!isProduction || AllowSeedInProduction);
}
