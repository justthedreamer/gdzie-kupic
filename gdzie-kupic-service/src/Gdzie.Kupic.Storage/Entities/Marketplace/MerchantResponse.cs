namespace Gdzie.Kupic.Storage.Entities.Marketplace;

public enum MerchantResponseState
{
    CantHelp,
    MayHaveIt,
    HaveIt,
    CanOrderIt,
}

public sealed class MerchantResponse(
    Guid id,
    Guid postId,
    Guid merchantId,
    MerchantResponseState state,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt)
{
    public Guid Id { get; init; } = id;
    public Guid PostId { get; init; } = postId;
    public Guid MerchantId { get; init; } = merchantId;
    public MerchantResponseState State { get; init; } = state;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; init; } = updatedAt;

    public Post Post { get; init; } = null!;
    public Merchant Merchant { get; init; } = null!;
}

