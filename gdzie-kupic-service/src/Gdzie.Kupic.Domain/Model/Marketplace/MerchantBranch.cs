namespace Gdzie.Kupic.Domain.Model.Marketplace;

using Gdzie.Kupic.Domain.Model.Location;

public sealed class MerchantBranch(
    Guid id,
    Guid merchantId,
    string displayName,
    Coordinates coordinates,
    string? phone,
    string? website,
    string? addressDisplayName,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid MerchantId { get; init; } = merchantId;
    public string DisplayName { get; set; } = displayName;
    public Coordinates Coordinates { get; set; } = coordinates;
    public string? Phone { get; set; } = phone;
    public string? Website { get; set; } = website;
    public string? AddressDisplayName { get; set; } = addressDisplayName;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; set; } = createdAt;
}