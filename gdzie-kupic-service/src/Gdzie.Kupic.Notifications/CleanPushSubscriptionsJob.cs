namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Removes a push subscription the push service reported as gone (FR-NOTIF-4); safe to run repeatedly.</summary>
internal sealed class CleanPushSubscriptionsJob(INotificationStorage storage, ILogger<CleanPushSubscriptionsJob> logger)
{
    public async Task RunAsync(string endpoint)
    {
        var removed = await storage.RemovePushSubscriptionByEndpointAsync(endpoint);

        logger.LogInformation("Push subscription cleanup: {Removed} removed", removed);
    }
}
