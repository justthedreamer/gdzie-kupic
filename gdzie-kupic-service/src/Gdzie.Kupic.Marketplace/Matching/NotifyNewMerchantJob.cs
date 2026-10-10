namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Notifies a merchant about already open posts that match them (after onboarding or a new
/// subscription). Safe to run repeatedly: posts the merchant was notified about are skipped.
/// </summary>
internal sealed class NotifyNewMerchantJob(
    IMatchingStorage matching,
    INotificationDispatcher dispatcher,
    PostFeedEvents events,
    TimeProvider clock,
    ILogger<NotifyNewMerchantJob> logger)
{
    public async Task RunAsync(Guid merchantId)
    {
        var now = clock.GetUtcNow();

        var postIds = await matching.FindMatchingOpenPostIdsAsync(merchantId, now);
        var created = await matching.AddMerchantNotificationsAsync(merchantId, postIds, now);

        foreach (var postId in created)
        {
            await dispatcher.DispatchAsync(postId, merchantId);
            await events.PostAddedAsync(merchantId, postId);
        }

        logger.LogInformation(
            "Merchant {MerchantId} notified about {Created} open posts ({Matched} matched)", merchantId, created.Count, postIds.Count);
    }
}
