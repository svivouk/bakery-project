using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reviews.Infrastructure.Persistence;
public sealed class MongoTransactionSupport(IMongoClient client, ILogger<MongoTransactionSupport> logger)
{
    private bool? _supported;

    public async Task<bool> IsSupportedAsync(CancellationToken ct = default)
    {
        if (_supported is { } known) return known;

        var hello = await client.GetDatabase("admin")
            .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);

        var isReplicaSet = hello.Contains("setName");
        var isMongos = hello.TryGetValue("msg", out var msg) && msg.IsString && msg.AsString == "isdbgrid";
        _supported = isReplicaSet || isMongos;

        if (_supported == false)
            logger.LogWarning(
                "MongoDB працює без replica set: транзакції недоступні, Unit of Work виконується без них. " +
                "Для локальної розробки запустіть docker/docker-compose.yml (одновузловий replica set).");
        else
            logger.LogInformation("MongoDB підтримує транзакції ({Topology}).", isMongos ? "sharded" : "replica set");

        return _supported.Value;
    }
}