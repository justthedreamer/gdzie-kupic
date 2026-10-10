namespace Gdzie.Kupic.Realtime;

using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Service.API.Contract.Realtime;

public sealed class NotificationChannel(IRealtimeSender sender) : INotificationChannel
{
    public Task NotificationRaisedAsync(Guid userId, NotificationKind kind, Guid? postId, Guid? threadId, CancellationToken ct = default)
    {
        var name = kind switch
        {
            NotificationKind.MerchantResponded => RealtimeEvents.NotificationKinds.MerchantResponded,
            NotificationKind.NewMessage => RealtimeEvents.NotificationKinds.NewMessage,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        return sender.SendToUserAsync(
            userId, RealtimeEvents.NotificationRaised, new RealtimeEvents.NotificationRaisedPayload(name, postId, threadId), ct);
    }
}
