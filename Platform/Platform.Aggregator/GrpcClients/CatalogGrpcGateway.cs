using Grpc.Core;
using Platform.Contracts.Catalog;

namespace Platform.Aggregator.GrpcClients;
public sealed class CatalogGrpcGateway(CatalogGrpc.CatalogGrpcClient client, ILogger<CatalogGrpcGateway> logger)
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    public async Task<GrpcResult<ProductDetailsInfo>> GetProductBySlugAsync(string slug, CancellationToken ct)
    {
        using var call = client.GetProductBySlugAsync(
            new GetProductBySlugRequest { Slug = slug },
            deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);
        return await ReadAsync(call, nameof(GetProductBySlugAsync));
    }
    public async Task<GrpcResult<GetProductsByIdsReply>> GetProductsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var request = new GetProductsByIdsRequest();
        request.ProductIds.AddRange(ids.Distinct().Select(i => i.ToString()));

        using var call = client.GetProductsByIdsAsync(request,
            deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);
        return await ReadAsync(call, nameof(GetProductsByIdsAsync));
    }

    private async Task<GrpcResult<T>> ReadAsync<T>(AsyncUnaryCall<T> call, string operation) where T : class
    {
        try
        {
            var reply = await call.ResponseAsync;
            var trailers = call.GetTrailers();
            var cache = trailers.GetValue("x-cache") ?? "n/a";
            logger.LogInformation("Catalog gRPC {Operation} ok, x-cache {Cache}", operation, cache);
            return new GrpcResult<T>(reply, cache);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("Catalog gRPC {Operation}: NotFound ({Detail})", operation, ex.Status.Detail);
            return new GrpcResult<T>(null, "none");
        }
    }
}
public sealed record GrpcResult<T>(T? Value, string Cache) where T : class;
