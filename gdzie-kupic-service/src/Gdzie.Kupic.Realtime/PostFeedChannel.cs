namespace Gdzie.Kupic.Realtime;

using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Realtime;

public sealed class PostFeedChannel(IRealtimeSender sender) : IPostFeedChannel
{
    public Task PostAddedAsync(Guid userId, Guid postId, CancellationToken ct = default) =>
        sender.SendToUserAsync(userId, RealtimeEvents.PostAdded, new RealtimeEvents.PostAddedPayload(postId), ct);

    public Task PostRemovedAsync(Guid userId, Guid postId, CancellationToken ct = default) =>
        sender.SendToUserAsync(userId, RealtimeEvents.PostRemoved, new RealtimeEvents.PostRemovedPayload(postId), ct);

    public Task PostStatusChangedAsync(Guid userId, Guid postId, CancellationToken ct = default) =>
        sender.SendToUserAsync(userId, RealtimeEvents.PostStatusChanged, new RealtimeEvents.PostStatusChangedPayload(postId), ct);
}
