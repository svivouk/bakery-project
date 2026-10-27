using MongoDB.Bson;
using Reviews.Domain.Entities;
using Reviews.Domain.Models;

namespace Reviews.Domain.Interfaces;
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, ReadConsistency consistency = ReadConsistency.Strong,
                                   CancellationToken ct = default);
    Task<Review?> GetByOrderItemIdAsync(Guid orderItemId, ReadConsistency consistency = ReadConsistency.Strong,
                                        CancellationToken ct = default);
    Task<Page<Review>> FindAsync(ReviewCriteria criteria, PageRequest page,
                                 ReadConsistency consistency = ReadConsistency.Eventual,
                                 CancellationToken ct = default);

    Task<long> CountAsync(ReviewCriteria criteria, CancellationToken ct = default);
    Task<IReadOnlyList<Review>> SearchTextAsync(string query, ReviewCriteria? filter, int limit,
                                            CancellationToken ct = default);
    Task<RatingStats> AggregateRatingStatsAsync(Guid productId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);
    Task UpdateAsync(Review review, CancellationToken ct = default);
    Task IncrementReportCountAsync(ObjectId reviewId, CancellationToken ct = default);
    Task<long> RenameProductAsync(Guid productId, string newName, CancellationToken ct = default);

}