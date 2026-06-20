namespace Gdzie.Kupic.Storage.Entities.Marketplace;

using Gdzie.Kupic.Storage.Entities.Auth;

public sealed class MerchantAccount(
    Guid id,
    Guid merchantId,
    Guid userId,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid MerchantId { get; init; } = merchantId;
    public Guid UserId { get; init; } = userId;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public Merchant Merchant { get; init; } = null!;
    public User User { get; init; } = null!;
}

