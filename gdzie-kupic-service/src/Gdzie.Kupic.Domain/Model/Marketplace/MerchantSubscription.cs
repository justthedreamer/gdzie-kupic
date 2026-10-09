namespace Gdzie.Kupic.Domain.Model.Marketplace;

public sealed class MerchantSubscription(
    Guid id,
    Guid merchantId,
    Guid categoryId,
    Guid? tagId,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid MerchantId { get; init; } = merchantId;
    public Guid CategoryId { get; init; } = categoryId;
    public Guid? TagId { get; init; } = tagId;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}