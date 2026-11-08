using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Clients;

public sealed class ReviewsClient(HttpClient http)
{
    public async Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var tasks = ids.Select(async id =>
        {
            var page = await http.GetFromJsonAsync<ReviewPageDto>(
                $"/api/reviews?productId={id}&pageSize=50", ct);
            return page?.Items ?? [];
        });
        return (await Task.WhenAll(tasks)).SelectMany(r => r).ToList();
    }
}
