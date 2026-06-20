namespace Gdzie.Kupic.Storage.Entities.Marketplace;

using Gdzie.Kupic.Storage.Entities.Catalogue;

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

    public Merchant Merchant { get; init; } = null!;
    public Category Category { get; init; } = null!;
    public Tag? Tag { get; init; }
}

