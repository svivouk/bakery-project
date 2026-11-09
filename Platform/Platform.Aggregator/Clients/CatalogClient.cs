using System.Globalization;
using Platform.Aggregator.Contracts;
using Platform.Aggregator.GrpcClients;

namespace Platform.Aggregator.Clients;

public sealed class CatalogClient(CatalogGrpcGateway gateway)
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];

        var result = await gateway.GetProductsByIdsAsync(idList, ct);
        return result.Value!.Products.Select(p => new ProductDto(
            Guid.Parse(p.ProductId),
            p.Name,
            p.Slug,
            string.IsNullOrEmpty(p.ImageUrl) ? null : p.ImageUrl,
            decimal.Parse(p.BasePrice, NumberStyles.Number, CultureInfo.InvariantCulture))).ToList();
    }
}
