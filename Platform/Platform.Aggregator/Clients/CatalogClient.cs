using Platform.Aggregator.Contracts;
using System.Net;

namespace Platform.Aggregator.Clients;

public sealed class CatalogClient(HttpClient http)
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