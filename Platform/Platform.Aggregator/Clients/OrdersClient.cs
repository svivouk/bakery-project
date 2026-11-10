using System.Net;
using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Clients;

/// <summary>HTTP-реалізація (Лаб. 05).</summary>
public sealed class OrdersClient(HttpClient http) : IOrdersSource
{
    public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/orders/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }
}
