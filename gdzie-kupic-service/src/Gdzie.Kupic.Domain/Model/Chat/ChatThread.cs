namespace Gdzie.Kupic.Domain.Model.Chat;

using Gdzie.Kupic.Domain.Model.Marketplace;

public sealed class ChatThread(
    Guid id,
    Guid postId,
    Guid merchantId,
    bool isLocked,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid PostId { get; init; } = postId;
    public Guid MerchantId { get; init; } = merchantId;
    public bool IsLocked { get; set; } = isLocked;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    /// <summary>Messages sent after these timestamps count as unread for the respective participant.</summary>
    public DateTimeOffset? BuyerLastReadAt { get; set; }

    public DateTimeOffset? MerchantLastReadAt { get; set; }

    public Post Post { get; init; } = null!;
    public ICollection<ChatMessage> Messages { get; init; } = [];
}