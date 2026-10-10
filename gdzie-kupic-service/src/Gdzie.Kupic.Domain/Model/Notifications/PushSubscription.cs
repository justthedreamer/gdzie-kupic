namespace Gdzie.Kupic.Domain.Model.Notifications;

using Auth;

/// <summary>A browser push registration of one device; identified by its (globally unique) endpoint.</summary>
public sealed class PushSubscription(
    Guid id,
    Guid userId,
    string endpoint,
    DateTimeOffset createdAt)
{
    public const int MaxEndpointLength = 2048;
    public const int MaxKeyLength = 256;

    public Guid Id { get; init; } = id;
    public Guid UserId { get; set; } = userId;
    public string Endpoint { get; init; } = endpoint;
    public required WebPushKeys Keys { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public User User { get; init; } = null!;
}
