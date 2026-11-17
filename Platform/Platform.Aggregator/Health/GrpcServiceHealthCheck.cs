using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthClient = Grpc.Health.V1.Health.HealthClient;

namespace Platform.Aggregator.Health;
public sealed class GrpcServiceHealthCheck(
    string serviceName,                    
    GrpcClientFactory clientFactory,
    ILogger<GrpcServiceHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = clientFactory.CreateClient<HealthClient>(serviceName);
            var reply = await client.CheckAsync(new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2), cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{serviceName} is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{serviceName} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} health check failed ({Status})", serviceName, ex.StatusCode);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{serviceName} is unavailable", ex);
        }
    }
}
