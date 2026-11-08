using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Interfaces;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence;
public sealed class MongoReviewEligibilityRepository(MongoDbContext db) : IReviewEligibilityRepository
{
    private static readonly FilterDefinitionBuilder<BsonDocument> F = Builders<BsonDocument>.Filter;
    private static readonly UpdateDefinitionBuilder<BsonDocument> U = Builders<BsonDocument>.Update;

    private IMongoCollection<BsonDocument> Collection => db.Collection<BsonDocument>(MongoCollections.ReviewEligibility);

    public async Task<ReviewEligibility?> GetByOrderItemIdAsync(Guid orderItemId,
        ReadConsistency consistency = ReadConsistency.Strong, CancellationToken ct = default)
    {
        var document = await FindAsync(F.Eq("orderItemId", Uuid(orderItemId)), ct).ConfigureAwait(false);
        return document.FirstOrDefault() is { } d ? ToModel(d) : null;
    }

    public async Task<IReadOnlyList<ReviewEligibility>> ListAvailableAsync(Guid customerId, CancellationToken ct = default)
    {
        var filter = F.And(F.Eq("customerId", Uuid(customerId)), F.Eq("reviewId", BsonNull.Value));
        var documents = await FindAsync(filter, ct).ConfigureAwait(false);
        return documents.Select(ToModel).OrderBy(e => e.EligibleSince).ToList();
    }

    public async Task UpsertAsync(ReviewEligibility eligibility, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        var filter = F.Eq("orderItemId", Uuid(eligibility.OrderItemId));
        var document = ToDocument(eligibility);
        var options = new ReplaceOptions { IsUpsert = true };

        if (db.Session is { } s) await Collection.ReplaceOneAsync(s, filter, document, options, ct);
        else await Collection.ReplaceOneAsync(filter, document, options, ct);
    }

    public async Task<bool> TryClaimAsync(Guid orderItemId, ObjectId reviewId, CancellationToken ct = default)
    {
        var filter = F.And(F.Eq("orderItemId", Uuid(orderItemId)), F.Eq("reviewId", BsonNull.Value));
        var update = U.Set("reviewId", reviewId);

        var result = db.Session is { } s
            ? await Collection.UpdateOneAsync(s, filter, update, cancellationToken: ct)
            : await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);

        return result.ModifiedCount == 1;
    }

    public async Task ReleaseAsync(Guid orderItemId, ObjectId reviewId, CancellationToken ct = default)
    {
        var filter = F.And(F.Eq("orderItemId", Uuid(orderItemId)), F.Eq("reviewId", reviewId));
        var update = U.Set("reviewId", BsonNull.Value);

        if (db.Session is { } s) await Collection.UpdateOneAsync(s, filter, update, cancellationToken: ct);
        else await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private async Task<List<BsonDocument>> FindAsync(FilterDefinition<BsonDocument> filter, CancellationToken ct)
    {
        var find = db.Session is { } s ? Collection.Find(s, filter) : Collection.Find(filter);
        return await find.ToListAsync(ct);
    }

    private static BsonBinaryData Uuid(Guid id) => new(id, GuidRepresentation.Standard);

    private static Guid ReadGuid(BsonDocument d, string name) => d[name].AsGuid;

    private static ReviewEligibility ToModel(BsonDocument d) => new(
        ReadGuid(d, "customerId"),
        ReadGuid(d, "productId"),
        ReadGuid(d, "orderItemId"),
        ReadGuid(d, "orderId"),
        d["eligibleSince"].ToUniversalTime(),
        d.TryGetValue("reviewId", out var reviewId) && reviewId.IsObjectId ? reviewId.AsObjectId : null);

    private static BsonDocument ToDocument(ReviewEligibility e) => new()
    {
        { "customerId", Uuid(e.CustomerId) },
        { "productId", Uuid(e.ProductId) },
        { "orderItemId", Uuid(e.OrderItemId) },
        { "orderId", Uuid(e.OrderId) },
        { "eligibleSince", e.EligibleSince },
        { "reviewId", e.ReviewId.HasValue ? e.ReviewId.Value : BsonNull.Value }
    };
}