namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Records and dispatches the notifications of one batch of matched merchants.</summary>
internal sealed class NotifyMerchantsBatchJob(
    IMatchingStorage matching,
    INotificationDispatcher dispatcher,
    TimeProvider clock,
    ILogger<NotifyMerchantsBatchJob> logger)
{
    public async Task RunAsync(Guid postId, List<Guid> merchantIds)
    {
        var created = await matching.AddNotificationsAsync(postId, merchantIds, clock.GetUtcNow());

        foreach (var merchantId in created)
            await dispatcher.DispatchAsync(postId, merchantId);

        logger.LogInformation(
            "Post {PostId}: {Created} of {Total} merchants newly notified", postId, created.Count, merchantIds.Count);
    }
}
