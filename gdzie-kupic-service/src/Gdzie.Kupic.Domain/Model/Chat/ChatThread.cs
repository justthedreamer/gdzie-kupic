namespace Gdzie.Kupic.Domain.Model.Chat;

using Gdzie.Kupic.Domain.Model.Marketplace;

// TODO: Re-link Merchant once it is reintroduced (Phase 3).
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
    public ICollection<ChatMessage> Messages { get; init; } = [];
}
