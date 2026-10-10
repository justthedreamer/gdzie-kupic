namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Hangfire;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Delivers one Web Push to one device. Transient failures throw, so Hangfire retries the job.</summary>
internal sealed class SendWebPushJob(
    INotificationStorage storage,
    IWebPushSender sender,
    IJobScheduler scheduler,
    IOptions<VapidSettings> vapid,
    TimeProvider clock,
    ILogger<SendWebPushJob> logger)
{
    public async Task RunAsync(Guid subscriptionId, NotificationKind kind, Guid? postId, Guid? threadId)
    {
        if (!vapid.Value.IsConfigured) return;

        var subscription = await storage.FindPushSubscriptionAsync(subscriptionId);
        if (subscription is null) return;

        var outcome = await sender.SendAsync(
            new WebPushTarget(subscription.Endpoint, subscription.P256dhKey, subscription.AuthKey),
            NotificationPayload.Build(kind, postId, threadId));

        switch (outcome)
        {
            case WebPushOutcome.Delivered:
                if (kind == NotificationKind.NewPost && postId is { } post)
                    await storage.MarkPostNotificationSentAsync(post, subscription.UserId, NotificationChannel.WebPush, clock.GetUtcNow());
                break;
            case WebPushOutcome.SubscriptionGone:
                var endpoint = subscription.Endpoint;
                scheduler.Enqueue<CleanPushSubscriptionsJob>(job => job.RunAsync(endpoint));
                break;
            default:
                logger.LogWarning("Web Push {Kind} to subscription {SubscriptionId} was refused", kind, subscriptionId);
                break;
        }
    }
}
