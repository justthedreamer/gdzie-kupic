namespace Gdzie.Kupic.Storage.Entities.Chat;

using Gdzie.Kupic.Storage.Entities.Marketplace;

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
    public bool IsLocked { get; init; } = isLocked;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public Post Post { get; init; } = null!;
    public Merchant Merchant { get; init; } = null!;
    public ICollection<ChatMessage> Messages { get; init; } = [];
}

