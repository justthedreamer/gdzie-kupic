namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Hangfire;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Delivery hierarchy of FR-NOTIF-2: a user with an open real-time connection already got the in-app event; any other
/// user gets a Web Push on every registered device, each delivered by its own background job, and an opted-in buyer an e-mail.
/// </summary>
internal sealed class NotificationDispatcher(
    INotificationStorage storage,
    IPresenceTracker presence,
    IJobScheduler scheduler,
    IOptions<VapidSettings> vapid,
    IOptions<AppLinksSettings> app,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(Notification notification, CancellationToken ct = default)
    {
        if (presence.IsOnline(notification.RecipientUserId)) return;

        await SafeAsync(notification, () => PushAsync(notification, ct));
        await SafeAsync(notification, () => EmailAsync(notification, ct));
    }

    private async Task PushAsync(Notification notification, CancellationToken ct)
    {
        if (!vapid.Value.IsConfigured) return;

        var postId = notification.PostId;
        var threadId = notification.ThreadId;
        var kind = notification.Kind;

        foreach (var subscriptionId in await storage.FindPushSubscriptionIdsAsync(notification.RecipientUserId, ct))
            scheduler.Enqueue<SendWebPushJob>(job => job.RunAsync(subscriptionId, kind, postId, threadId));
    }

    /// <summary>E-mail is for buyers only: a merchant response, or the first unread message of a series.</summary>
    private async Task EmailAsync(Notification notification, CancellationToken ct)
    {
        var wanted = notification.Kind == NotificationKind.MerchantResponded
                     || (notification.Kind == NotificationKind.NewMessage && notification.StartsUnreadSeries);
        if (!wanted) return;

        var recipient = await storage.FindEmailRecipientAsync(notification.RecipientUserId, ct);
        if (!SendEmailJob.IsEligible(recipient)) return;

        var userId = notification.RecipientUserId;
        var postId = notification.PostId;
        var threadId = notification.ThreadId;
        var kind = notification.Kind;
        scheduler.Enqueue<SendEmailJob>(job => job.RunAsync(userId, kind, postId, threadId));
    }

    private async Task SafeAsync(Notification notification, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispatch {Kind} notification", notification.Kind);
        }
    }
}
