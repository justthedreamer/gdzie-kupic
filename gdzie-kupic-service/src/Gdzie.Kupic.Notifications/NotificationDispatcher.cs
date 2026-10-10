namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Hangfire;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Delivery hierarchy of FR-NOTIF-2: a user with an open real-time connection already got the in-app event; any other
/// user gets a Web Push on every registered device, each delivered by its own background job.
/// </summary>
internal sealed class NotificationDispatcher(
    INotificationStorage storage,
    IPresenceTracker presence,
    IJobScheduler scheduler,
    IOptions<VapidSettings> vapid,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(Notification notification, CancellationToken ct = default)
    {
        try
        {
            if (!vapid.Value.IsConfigured || presence.IsOnline(notification.RecipientUserId)) return;

            var postId = notification.PostId;
            var threadId = notification.ThreadId;
            var kind = notification.Kind;

            foreach (var subscriptionId in await storage.FindPushSubscriptionIdsAsync(notification.RecipientUserId, ct))
                scheduler.Enqueue<SendWebPushJob>(job => job.RunAsync(subscriptionId, kind, postId, threadId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispatch {Kind} notification", notification.Kind);
        }
    }
}
