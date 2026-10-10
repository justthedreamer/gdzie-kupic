namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Records and dispatches the notifications of one batch of matched merchants.</summary>
internal sealed class NotifyMerchantsBatchJob(
    IMatchingStorage matching,
    PostFeedEvents events,
    TimeProvider clock,
    ILogger<NotifyMerchantsBatchJob> logger)
{
    public async Task RunAsync(Guid postId, List<Guid> merchantIds)
    {
        var created = await matching.AddNotificationsAsync(postId, merchantIds, clock.GetUtcNow());

        foreach (var merchantId in created)
        {
            await events.PostAddedAsync(merchantId, postId);
        }

        logger.LogInformation(
            "Post {PostId}: {Created} of {Total} merchants newly notified", postId, created.Count, merchantIds.Count);
    }
}
