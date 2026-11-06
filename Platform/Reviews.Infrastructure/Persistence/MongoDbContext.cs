using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IClientSessionHandle? Session { get; internal set; }

    public MongoDbContext(IMongoClient client, IOptions<MongoDbSettings> options)
    {
        MongoMappings.Register();                      

        Client = client;
        Database = client.GetDatabase(options.Value.DatabaseName, new MongoDatabaseSettings
        {
            WriteConcern = WriteConcern.WMajority
        });
    }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(MongoCollections.Reviews);
    public IMongoCollection<TDocument> Collection<TDocument>(string name) =>
        Database.GetCollection<TDocument>(name);
}