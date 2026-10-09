namespace Gdzie.Kupic.Domain.Model.Marketplace;

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
}