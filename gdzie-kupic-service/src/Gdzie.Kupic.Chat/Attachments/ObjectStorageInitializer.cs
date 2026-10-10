namespace Gdzie.Kupic.Chat;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class ObjectStorageInitializer(
    IObjectStorage storage,
    IOptions<StorageSettings> settings,
    ILogger<ObjectStorageInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!settings.Value.AutoCreateBucket) return;

        try
        {
            await storage.EnsureBucketAsync(ct);
            logger.LogInformation("Attachment bucket '{Bucket}' is ready", settings.Value.BucketName);
        }
        catch (Exception ex)
        {
            // Not fatal: the health check keeps reporting the problem and uploads fail until storage is reachable.
            logger.LogWarning(ex, "Could not ensure attachment bucket '{Bucket}'", settings.Value.BucketName);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}