using System.Net;
using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Clients;

/// <summary>HTTP-реалізація (Лаб. 05): по одному запиту на товар.</summary>
public sealed class CatalogClient(HttpClient http) : ICatalogSource
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var tasks = ids.Select(async id =>
        {
            using var response = await http.GetAsync($"/api/products/{id}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductDto>(ct);
        });
        return (await Task.WhenAll(tasks)).Where(p => p is not null).Select(p => p!).ToList();
    }
}
