namespace Gdzie.Kupic.Domain.Model.Notifications;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum NotificationChannel
{
    WebPush,
    Email,
}

public sealed class PostNotification(
    Guid id,
    Guid postId,
    Guid merchantId,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid PostId { get; init; } = postId;
    public Guid MerchantId { get; init; } = merchantId;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    /// <summary>Empty until a real channel dispatches the notification (Phase 7).</summary>
    public NotificationChannel? Channel { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public Post Post { get; init; } = null!;
}
