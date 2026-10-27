namespace Orders.Api.Middlewear;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public bool MigrateOnStartup { get; set; }
    public bool SeedOnStartup { get; set; }
}

