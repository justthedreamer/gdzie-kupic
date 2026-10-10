namespace Gdzie.Kupic.Service.API;

using Gdzie.Kupic.Chat;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class ObjectStorageHealthCheck(IObjectStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await storage.IsReachableAsync(ct)
            ? HealthCheckResult.Healthy("Attachment bucket is reachable.")
            : HealthCheckResult.Unhealthy("Attachment bucket is not reachable.");
}