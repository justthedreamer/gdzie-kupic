namespace Gdzie.Kupic.Storage.Entities.Marketplace;

using Gdzie.Kupic.Storage.Entities.Common;
using Gdzie.Kupic.Storage.Entities.Location;

public sealed class MerchantBranch(
    Guid id,
    Guid merchantId,
    string displayName,
    Coordinates coordinates,
    string? addressDisplayName,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt)
{
    public Guid Id { get; init; } = id;
    public Guid MerchantId { get; init; } = merchantId;
    public string DisplayName { get; init; } = displayName;
    public Coordinates Coordinates { get; init; } = coordinates;
    public string? AddressDisplayName { get; init; } = addressDisplayName;
    public ContactDetails? ContactDetails { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; init; } = updatedAt;

    public Merchant Merchant { get; init; } = null!;
}