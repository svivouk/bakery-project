using Reviews.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Interfaces;

public interface IProductRatingSummaryRepository
{
    Task<ProductRatingSummary?> GetAsync(Guid productId, ReadConsistency consistency = ReadConsistency.Eventual,
                                         CancellationToken ct = default);
    Task<ProductRatingSummary> ApplyAsync(Guid productId, RatingDelta delta, CancellationToken ct = default);
    Task<ProductRatingSummary> ReplaceAsync(Guid productId, RatingStats stats, CancellationToken ct = default);
}
