using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Models;

namespace Reviews.Application.Common.Interfaces;
public interface IReviewQueries
{   
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<ReviewDto>> GetListAsync(ReviewCriteria criteria, int page, int pageSize, CancellationToken ct);
}