namespace Gdzie.Kupic.Service.API.Contract.Realtime;

/// <summary>
/// Server-to-client events of the single SignalR hub (<c>/hubs/app</c>). Events are thin: they carry identifiers only
/// and the client fetches the current state through REST. Names and property names are camelCase on the wire.
/// </summary>
public static class RealtimeEvents
{
    public const string HubPath = "/hubs/app";

    public const string PostAdded = "postAdded";
    public const string PostRemoved = "postRemoved";
    public const string PostStatusChanged = "postStatusChanged";
    public const string MessageReceived = "messageReceived";
    public const string ThreadUpdated = "threadUpdated";
    public const string NotificationRaised = "notificationRaised";

    public static class NotificationKinds
    {
        public const string MerchantResponded = "merchantResponded";
        public const string NewMessage = "newMessage";
    }

    public sealed record PostAddedPayload(Guid PostId);

    public sealed record PostRemovedPayload(Guid PostId);

    public sealed record PostStatusChangedPayload(Guid PostId);

    public sealed record MessageReceivedPayload(Guid ThreadId, Guid MessageId);

    public sealed record ThreadUpdatedPayload(Guid ThreadId);

    /// <summary><see cref="Kind"/> is one of <see cref="NotificationKinds"/>.</summary>
    public sealed record NotificationRaisedPayload(string Kind, Guid? PostId, Guid? ThreadId);
}