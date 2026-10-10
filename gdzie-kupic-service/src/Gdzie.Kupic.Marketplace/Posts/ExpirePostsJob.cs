namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Periodic job moving overdue Active posts to Expired; safe to run repeatedly.</summary>
internal sealed class ExpirePostsJob(IPostStorage posts, PostFeedEvents events, TimeProvider clock, ILogger<ExpirePostsJob> logger)
{
    public async Task RunAsync()
    {
        var expired = await posts.ExpireOverduePostsAsync(clock.GetUtcNow());

        foreach (var postId in expired)
        {
            await events.PostRemovedAsync(postId);
            await events.PostStatusChangedAsync(postId);
        }

        logger.LogInformation("Expired {Count} overdue posts", expired.Count);
    }
}
