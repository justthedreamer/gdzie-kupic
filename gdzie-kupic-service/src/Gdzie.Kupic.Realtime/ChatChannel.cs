namespace Gdzie.Kupic.Realtime;

using Gdzie.Kupic.Chat;
using Gdzie.Kupic.Service.API.Contract.Realtime;

public sealed class ChatChannel(IRealtimeSender sender) : IChatChannel
{
    public Task MessageReceivedAsync(Guid userId, Guid threadId, Guid messageId, CancellationToken ct = default) =>
        sender.SendToUserAsync(userId, RealtimeEvents.MessageReceived, new RealtimeEvents.MessageReceivedPayload(threadId, messageId), ct);

    public Task ThreadUpdatedAsync(Guid userId, Guid threadId, CancellationToken ct = default) =>
        sender.SendToUserAsync(userId, RealtimeEvents.ThreadUpdated, new RealtimeEvents.ThreadUpdatedPayload(threadId), ct);
}
