using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence;
public sealed class MongoProductRatingSummaryRepository(MongoDbContext db) : IProductRatingSummaryRepository
{
    private const int MaxAttempts = 5;
    private static readonly FilterDefinitionBuilder<BsonDocument> F = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Collection => db.Collection<BsonDocument>(MongoCollections.ProductRatingSummaries);

    public async Task<ProductRatingSummary?> GetAsync(Guid productId, ReadConsistency consistency = ReadConsistency.Eventual,
        CancellationToken ct = default)
    {
        var document = await FindOneAsync(productId, ct);
        return document is null ? null : ToModel(document);
    }

    public async Task<ProductRatingSummary> ApplyAsync(Guid productId, RatingDelta delta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(delta);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var currentDocument = await FindOneAsync(productId, ct);
            var current = currentDocument is null ? null : ToModel(currentDocument);

            var stats = Apply(current?.Stats ?? RatingStats.Empty, delta);
            var next = new ProductRatingSummary(productId, stats, (current?.Version ?? 0) + 1, DateTime.UtcNow);

            if (await TryWriteAsync(next, current?.Version, ct)) return next;
        }

        throw new InvalidOperationException(
            $"Не вдалося оновити зведення рейтингу товару {productId}: забагато конфліктів версій.");
    }

    public async Task<ProductRatingSummary> ReplaceAsync(Guid productId, RatingStats stats, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stats);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var currentDocument = await FindOneAsync(productId, ct);
            var current = currentDocument is null ? null : ToModel(currentDocument);
            var next = new ProductRatingSummary(productId, stats, (current?.Version ?? 0) + 1, DateTime.UtcNow);

            if (await TryWriteAsync(next, current?.Version, ct)) return next;
        }

        throw new InvalidOperationException(
            $"Не вдалося перерахувати зведення рейтингу товару {productId}: забагато конфліктів версій.");
    }

    // ------------------------------------------------------------ допоміжне

    /// <summary>Нове зведення: вставка (версія 1) або заміна з перевіркою попередньої версії. false — гонка, треба повторити.</summary>
    private async Task<bool> TryWriteAsync(ProductRatingSummary next, long? expectedVersion, CancellationToken ct)
    {
        var document = ToDocument(next);

        if (expectedVersion is null)
        {
            try
            {
                if (db.Session is { } s) await Collection.InsertOneAsync(s, document, cancellationToken: ct);
                else await Collection.InsertOneAsync(document, cancellationToken: ct);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;   // паралельний запит устиг створити зведення
            }
        }

        var filter = F.And(F.Eq("_id", Uuid(next.ProductId)), F.Eq("version", expectedVersion.Value));
        var result = db.Session is { } session
            ? await Collection.ReplaceOneAsync(session, filter, document, new ReplaceOptions(), ct)
            : await Collection.ReplaceOneAsync(filter, document, new ReplaceOptions(), ct);

        return result.MatchedCount == 1;
    }

    private static RatingStats Apply(RatingStats stats, RatingDelta delta)
    {
        var distribution = Enumerable.Range(Review.MinRating, Review.MaxRating - Review.MinRating + 1)
            .ToDictionary(star => star, star => stats.Distribution.TryGetValue(star, out var n) ? n : 0);
        var count = stats.Count;
        var sum = stats.Sum;

        if (delta.Removed is { } removed)
        {
            distribution[removed] = Math.Max(0, distribution[removed] - 1);
            count = Math.Max(0, count - 1);
            sum = Math.Max(0, sum - removed);
        }

        if (delta.Added is { } added)
        {
            distribution[added]++;
            count++;
            sum += added;
        }

        return new RatingStats(count, sum, distribution);
    }

    private async Task<BsonDocument?> FindOneAsync(Guid productId, CancellationToken ct)
    {
        var filter = F.Eq("_id", Uuid(productId));
        var find = db.Session is { } s ? Collection.Find(s, filter) : Collection.Find(filter);
        return await find.FirstOrDefaultAsync(ct);
    }

    private static BsonBinaryData Uuid(Guid id) => new(id, GuidRepresentation.Standard);

    private static ProductRatingSummary ToModel(BsonDocument d)
    {
        var distribution = new Dictionary<int, int>();
        var source = d["distribution"].AsBsonDocument;
        for (var star = Review.MinRating; star <= Review.MaxRating; star++)
            distribution[star] = source.TryGetValue(star.ToString(), out var n) ? n.ToInt32() : 0;

        var stats = new RatingStats(d["count"].ToInt32(), d["sum"].ToInt32(), distribution);
        return new ProductRatingSummary(d["_id"].AsGuid, stats, d["version"].ToInt64(), d["updatedAt"].ToUniversalTime());
    }

    private static BsonDocument ToDocument(ProductRatingSummary summary)
    {
        var distribution = new BsonDocument();
        for (var star = Review.MinRating; star <= Review.MaxRating; star++)
            distribution.Add(star.ToString(), new BsonInt32(summary.Stats.Distribution.TryGetValue(star, out var n) ? n : 0));

        return new BsonDocument
        {
            { "_id", Uuid(summary.ProductId) },
            { "count", new BsonInt32(summary.Stats.Count) },
            { "sum", new BsonInt32(summary.Stats.Sum) },
            { "average", new BsonDouble(summary.Stats.Average) },
            { "distribution", distribution },
            { "version", new BsonInt64(summary.Version) },
            { "updatedAt", summary.UpdatedAt }
        };
    }
}