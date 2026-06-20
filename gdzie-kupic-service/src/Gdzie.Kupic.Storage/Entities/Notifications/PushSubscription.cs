namespace Gdzie.Kupic.Storage.Entities.Notifications;

using Gdzie.Kupic.Storage.Entities.Auth;

public sealed class PushSubscription(
    Guid id,
    Guid userId,
    string endpoint,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string Endpoint { get; init; } = endpoint;
    public required WebPushKeys Keys { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public User User { get; init; } = null!;
}