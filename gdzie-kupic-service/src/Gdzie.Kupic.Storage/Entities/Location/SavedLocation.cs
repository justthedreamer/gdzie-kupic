namespace Gdzie.Kupic.Storage.Entities.Location;

using Gdzie.Kupic.Storage.Entities.Auth;

public sealed class SavedLocation(
    Guid id,
    Guid buyerProfileId,
    string displayName,
    Coordinates coordinates,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid BuyerProfileId { get; init; } = buyerProfileId;
    public string DisplayName { get; init; } = displayName;
    public Coordinates Coordinates { get; init; } = coordinates;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public BuyerProfile BuyerProfile { get; init; } = null!;
}