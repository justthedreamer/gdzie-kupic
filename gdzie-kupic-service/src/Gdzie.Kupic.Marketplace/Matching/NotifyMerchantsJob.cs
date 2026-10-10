namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Hangfire;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Finds the merchants a post reaches and fans the work out into independently retried batches.
/// Idempotent: a repeated run enqueues batches that find every notification already recorded.
/// </summary>
internal sealed class NotifyMerchantsJob(
    IPostStorage posts,
    IMatchingStorage matching,
    IJobScheduler scheduler,
    TimeProvider clock,
    ILogger<NotifyMerchantsJob> logger)
{
    public const int BatchSize = 50;

    public async Task RunAsync(Guid postId)
    {
        var post = await posts.FindForUpdateAsync(postId);
        if (post is null)
        {
            logger.LogWarning("Post {PostId} not found, nothing to notify", postId);
            return;
        }

        var merchantIds = post.Status == PostStatus.Active
            ? await matching.FindMatchingMerchantIdsAsync(
                new PostMatchCriteria(post.Id, post.BuyerId, post.Coordinates, post.RadiusKm, post.CategoryId, post.TagId))
            : [];

        foreach (var batch in merchantIds.Chunk(BatchSize))
        {
            var ids = batch.ToList();
            scheduler.Enqueue<NotifyMerchantsBatchJob>(job => job.RunAsync(postId, ids));
        }

        post.MarkDispatched(clock.GetUtcNow());
        await posts.SaveChangesAsync();

        logger.LogInformation("Post {PostId} matched {MerchantCount} merchants", postId, merchantIds.Count);
    }
}
