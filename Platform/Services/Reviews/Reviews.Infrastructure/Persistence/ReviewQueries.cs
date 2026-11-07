using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Entities;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence;
public sealed class ReviewQueries : IReviewQueries
{
    private static readonly ProjectionDefinition<Review, BsonDocument> Projection = new BsonDocument
    {
        { "productId", 1 }, { "productName", 1 }, { "author.displayName", 1 },
        { "rating", 1 }, { "text", 1 }, { "status", 1 }, { "version", 1 }, { "createdAt", 1 },
        { "reply.text", 1 }, { "reply.author.displayName", 1 }, { "reply.createdAt", 1 },
        { "photosCount", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$photos", new BsonArray() })) }
    };

    private readonly IMongoCollection<Review> _primary;     
    private readonly IMongoCollection<Review> _secondary;   

    public ReviewQueries(MongoDbContext db)
    {
        _primary = db.Reviews;
        _secondary = db.Reviews.WithReadPreference(ReadPreference.SecondaryPreferred);
    }

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var filter = Builders<Review>.Filter.And(
            Builders<Review>.Filter.Eq(x => x.Id, objectId),
            Builders<Review>.Filter.Eq(x => x.IsDeleted, false));

        var doc = await _primary.Find(filter).Project(Projection).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDto(doc);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        ReviewCriteria criteria, int page, int pageSize, CancellationToken ct)
    {
        var filter = ReviewFilters.Build(criteria);
        var skip = (page - 1) * pageSize;

        var pageTask = _secondary.Aggregate()
            .Match(filter)
            .Sort(ReviewFilters.Sort(criteria.Sort))
            .Skip(skip)
            .Limit(pageSize)
            .Project<BsonDocument>(Projection)
            .ToListAsync(ct);

        var countTask = _secondary.CountDocumentsAsync(filter, cancellationToken: ct);

        await Task.WhenAll(pageTask, countTask);

        return new PagedResult<ReviewDto>(pageTask.Result.Select(ToDto).ToList(), page, pageSize, countTask.Result);
    }

    private static ReviewDto ToDto(BsonDocument d)
    {
        ReplyDto? reply = null;
        if (d.TryGetValue("reply", out var r) && r.IsBsonDocument)
        {
            var rd = r.AsBsonDocument;
            reply = new ReplyDto(
                rd.GetValue("text", "").AsString,
                rd["author"]["displayName"].AsString,
                rd["createdAt"].ToUniversalTime());
        }

        return new ReviewDto(
            Id: d["_id"].AsObjectId.ToString(),
            ProductId: d["productId"].AsGuid,
            ProductName: d["productName"].AsString,
            AuthorName: d["author"]["displayName"].AsString,
            Rating: d["rating"].AsInt32,
            Text: d.TryGetValue("text", out var t) && t.IsString ? t.AsString : null,
            Status: d["status"].AsString,
            PhotosCount: d["photosCount"].AsInt32,
            Reply: reply,
            CreatedAt: d["createdAt"].ToUniversalTime(),
            Version: d["version"].AsInt32);
    }
}