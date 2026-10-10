namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves the recipients of post events and pushes them through <see cref="IPostFeedChannel"/>.
/// Call only after the change is committed. A failure is logged and never propagates.
/// </summary>
internal sealed class PostFeedEvents(
    IPostStorage posts,
    IPostFeedChannel channel,
    INotificationDispatcher dispatcher,
    ILogger<PostFeedEvents> logger)
{
    /// <summary>The merchant was newly notified: in-app <c>postAdded</c> and, for offline users, a Web Push.</summary>
    public Task PostAddedAsync(Guid merchantId, Guid postId) =>
        SafeAsync("postAdded", postId, async () =>
        {
            foreach (var userId in await posts.FindMerchantUserIdsAsync(merchantId))
            {
                await channel.PostAddedAsync(userId, postId);
                await dispatcher.DispatchAsync(new Notification(NotificationKind.NewPost, userId, postId));
            }
        });

    public Task PostRemovedAsync(Guid postId) =>
        SafeAsync("postRemoved", postId, async () =>
        {
            foreach (var userId in await posts.FindNotifiedUserIdsAsync(postId))
                await channel.PostRemovedAsync(userId, postId);
        });

    public Task PostStatusChangedAsync(Guid postId, Guid? ownerId = null) =>
        SafeAsync("postStatusChanged", postId, async () =>
        {
            var owner = ownerId ?? await posts.FindOwnerIdAsync(postId);
            if (owner is { } userId) await channel.PostStatusChangedAsync(userId, postId);
        });

    private async Task SafeAsync(string eventName, Guid postId, Func<Task> push)
    {
        try
        {
            await push();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push {Event} for post {PostId}", eventName, postId);
        }
    }
}
