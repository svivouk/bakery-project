using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Infrastructure.Persistence;

public sealed class MongoProductNameVersionStore(MongoDbContext context) : IProductNameVersionStore
{
    private const string CollectionName = "productNameVersions";

    public async Task<bool> TryAdvanceAsync(Guid productId, long version, CancellationToken ct = default)
    {
        var collection = context.Collection<BsonDocument>(CollectionName);
        var id = new BsonBinaryData(productId, GuidRepresentation.Standard);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);

        var existing = context.Session is null
            ? await collection.Find(filter).FirstOrDefaultAsync(ct)
            : await collection.Find(context.Session, filter).FirstOrDefaultAsync(ct);

        if (existing is not null && existing["version"].ToInt64() >= version)
            return false;

        var document = new BsonDocument { { "_id", id }, { "version", version } };
        var options = new ReplaceOptions { IsUpsert = true };

        if (context.Session is null)
            await collection.ReplaceOneAsync(filter, document, options, ct);
        else
            await collection.ReplaceOneAsync(context.Session, filter, document, options, ct);

        return true;
    }
}

