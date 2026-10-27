using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Models;

namespace Reviews.Infrastructure.Persistence;
internal static class ReviewFilters
{
    public static FilterDefinition<Review> Build(ReviewCriteria c)
    {
        var f = Builders<Review>.Filter;
        var parts = new List<FilterDefinition<Review>>();

        if (!c.IncludeDeleted) parts.Add(f.Eq(x => x.IsDeleted, false));

        if (c.ProductId is { } productId) parts.Add(f.Eq(x => x.ProductId, productId));
        if (c.CustomerId is { } customerId) parts.Add(f.Eq(x => x.Author.Id, customerId));
        if (c.Status is { } status) parts.Add(f.Eq(x => x.Status, status));
        if (c.MinRating is { } min) parts.Add(f.Gte(x => x.Rating, min));
        if (c.MaxRating is { } max) parts.Add(f.Lte(x => x.Rating, max));
        if (c.CreatedFrom is { } from) parts.Add(f.Gte(x => x.CreatedAt, from));
        if (c.CreatedTo is { } to) parts.Add(f.Lt(x => x.CreatedAt, to));
        if (c.HasPhotos is { } hasPhotos) parts.Add(f.Exists("photos.0", hasPhotos));
        if (c.HasReply is { } hasReply) parts.Add(f.Exists(x => x.Reply, hasReply));

        return parts.Count == 0 ? FilterDefinition<Review>.Empty : f.And(parts);
    }

    public static SortDefinition<Review> Sort(ReviewSort sort)
    {
        var s = Builders<Review>.Sort;
        return sort switch
        {
            ReviewSort.Oldest => s.Ascending(x => x.CreatedAt).Ascending(x => x.Id),
            ReviewSort.RatingHigh => s.Descending(x => x.Rating).Descending(x => x.CreatedAt).Descending(x => x.Id),
            ReviewSort.RatingLow => s.Ascending(x => x.Rating).Descending(x => x.CreatedAt).Descending(x => x.Id),
            _ => s.Descending(x => x.CreatedAt).Descending(x => x.Id)
        };
    }
}