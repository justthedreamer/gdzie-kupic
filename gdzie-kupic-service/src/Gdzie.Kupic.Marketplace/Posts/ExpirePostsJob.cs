namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Periodic job moving overdue Active posts to Expired; safe to run repeatedly.</summary>
internal sealed class ExpirePostsJob(IPostStorage posts, TimeProvider clock, ILogger<ExpirePostsJob> logger)
{
    public async Task RunAsync()
    {
        var expired = await posts.ExpireOverduePostsAsync(clock.GetUtcNow());

        logger.LogInformation("Expired {Count} overdue posts", expired);
    }
}
