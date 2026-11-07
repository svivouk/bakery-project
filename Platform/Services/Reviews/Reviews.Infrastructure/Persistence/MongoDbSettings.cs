namespace Reviews.Infrastructure.Persistence;
public enum TransactionMode
{
    Auto,
    Enabled,
    Disabled
}


public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "reviews_db";
    public TransactionMode Transactions { get; set; } = TransactionMode.Auto;
}