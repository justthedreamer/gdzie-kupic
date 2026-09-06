namespace Gdzie.Kupic.Domain.Model.Location;

// TODO: Re-link to a buyer/profile model once it is reintroduced (Phase 3).
public sealed class SavedLocation(
    Guid id,
    Guid buyerId,
    string displayName,
    Coordinates coordinates,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid BuyerId { get; init; } = buyerId;
    public string DisplayName { get; init; } = displayName;
    public Coordinates Coordinates { get; init; } = coordinates;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}
