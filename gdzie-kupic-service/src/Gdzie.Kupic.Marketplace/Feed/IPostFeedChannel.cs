namespace Gdzie.Kupic.Marketplace;

/// <summary>
/// Real-time push of post events (implemented by the Realtime module). Recipients are user ids;
/// implementations must not be relied on for correctness - the client refetches over REST.
/// </summary>
public interface IPostFeedChannel
{
    /// <summary>A merchant account was newly notified about the post.</summary>
    Task PostAddedAsync(Guid userId, Guid postId, CancellationToken ct = default);

    /// <summary>The post left the merchant feed (closed, fulfilled or expired).</summary>
    Task PostRemovedAsync(Guid userId, Guid postId, CancellationToken ct = default);

    /// <summary>The status panel data of the post changed; sent to the post owner.</summary>
    Task PostStatusChangedAsync(Guid userId, Guid postId, CancellationToken ct = default);
}

internal sealed class NullPostFeedChannel : IPostFeedChannel
{
    public Task PostAddedAsync(Guid userId, Guid postId, CancellationToken ct = default) => Task.CompletedTask;
    public Task PostRemovedAsync(Guid userId, Guid postId, CancellationToken ct = default) => Task.CompletedTask;
    public Task PostStatusChangedAsync(Guid userId, Guid postId, CancellationToken ct = default) => Task.CompletedTask;
}
