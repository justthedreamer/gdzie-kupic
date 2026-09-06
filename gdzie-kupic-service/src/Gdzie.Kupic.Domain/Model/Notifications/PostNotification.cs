namespace Gdzie.Kupic.Domain.Model.Notifications;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum NotificationChannel
{
    WebPush,
    Email,
}

// TODO: Re-link Merchant once it is reintroduced (Phase 3).
public sealed class PostNotification(
    Guid id,
    Guid postId,
    Guid merchantId,
    NotificationChannel channel,
    DateTimeOffset sentAt)
{
    public Guid Id { get; init; } = id;
    public Guid PostId { get; init; } = postId;
    public Guid MerchantId { get; init; } = merchantId;
    public NotificationChannel Channel { get; init; } = channel;
    public DateTimeOffset SentAt { get; init; } = sentAt;

    public Post Post { get; init; } = null!;
}
