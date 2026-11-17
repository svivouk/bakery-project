using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;
public sealed class StartupState
{
    private volatile bool _completed;
    public bool IsCompleted => _completed;
    public void MarkCompleted() => _completed = true;
}

public sealed class StartupHealthCheck(StartupState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(state.IsCompleted
            ? HealthCheckResult.Healthy("startup completed")
            : HealthCheckResult.Unhealthy("startup in progress"));
}