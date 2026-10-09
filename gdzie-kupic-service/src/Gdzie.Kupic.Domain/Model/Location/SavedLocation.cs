namespace Gdzie.Kupic.Domain.Model.Location;

public sealed class SavedLocation(
    Guid id,
    Guid userId,
    string displayName,
    Coordinates coordinates,
    string? addressDisplayName,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string DisplayName { get; init; } = displayName;
    public Coordinates Coordinates { get; init; } = coordinates;
    /// <summary>Human-readable address (e.g. "31-042 Kraków, Polska"); null when it could not be determined.</summary>
    public string? AddressDisplayName { get; init; } = addressDisplayName;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}