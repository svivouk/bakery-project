using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

public sealed class MongoInbox(MongoDbContext context) : IInbox
{
    public async Task<bool> TryRegisterAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        var collection = context.Collection<BsonDocument>(MongoCollections.Inbox);
        var id = new BsonBinaryData(eventId, GuidRepresentation.Standard);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);

        var exists = context.Session is null
            ? await collection.Find(filter).AnyAsync(ct)
            : await collection.Find(context.Session, filter).AnyAsync(ct);
        if (exists) return false;

        var document = new BsonDocument
        {
            { "_id", id },
            { "eventType", eventType },
            { "processedAt", DateTime.UtcNow }
        };

        if (context.Session is null)
            await collection.InsertOneAsync(document, cancellationToken: ct);
        else
            await collection.InsertOneAsync(context.Session, document, cancellationToken: ct);

        return true;
    }
}
