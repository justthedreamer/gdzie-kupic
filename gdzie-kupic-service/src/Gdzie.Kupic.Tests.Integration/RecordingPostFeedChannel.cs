using System.Collections.Concurrent;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Realtime;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>Records every post event and forwards it to the real channel, so a connected SignalR client still receives it.</summary>
public sealed class RecordingPostFeedChannel(IRealtimeSender sender) : IPostFeedChannel
{
    private readonly PostFeedChannel _inner = new(sender);
    private readonly ConcurrentQueue<(string Event, Guid UserId, Guid PostId)> _events = new();

    public IReadOnlyList<(string Event, Guid UserId, Guid PostId)> Events => _events.ToArray();

    public Task PostAddedAsync(Guid userId, Guid postId, CancellationToken ct = default) =>
        Record("postAdded", userId, postId, _inner.PostAddedAsync(userId, postId, ct));

    public Task PostRemovedAsync(Guid userId, Guid postId, CancellationToken ct = default) =>
        Record("postRemoved", userId, postId, _inner.PostRemovedAsync(userId, postId, ct));

    public Task PostStatusChangedAsync(Guid userId, Guid postId, CancellationToken ct = default) =>
        Record("postStatusChanged", userId, postId, _inner.PostStatusChangedAsync(userId, postId, ct));

    public void Reset() => _events.Clear();

    private Task Record(string name, Guid userId, Guid postId, Task forwarded)
    {
        _events.Enqueue((name, userId, postId));
        return forwarded;
    }
}
