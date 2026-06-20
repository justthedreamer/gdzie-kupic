namespace Gdzie.Kupic.Storage.Entities.Notifications;

using Gdzie.Kupic.Storage.Entities.Marketplace;

public enum NotificationChannel
{
    WebPush,
    Email,
}

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
    public Merchant Merchant { get; init; } = null!;
}

