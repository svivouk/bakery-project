using Platform.Aggregator.Contracts;
using System.Net;

namespace Platform.Aggregator.Clients
{
    public sealed class OrdersClient(HttpClient http)
    {
        public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct)
        {
            using var response = await http.GetAsync($"/api/orders/{id}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
        }
    }
}
