namespace Orders.Api.Middleware;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public bool MigrateOnStartup { get; set; }
    public bool SeedOnStartup { get; set; }
    public bool AllowSeedInProduction { get; set; }

    public bool ShouldSeed(bool isProduction) => SeedOnStartup && (!isProduction || AllowSeedInProduction);
}

