namespace Gdzie.Kupic.Domain.Model.Location;

public sealed class SavedLocation(
    Guid id,
    Guid userId,
    string displayName,
    Coordinates coordinates,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string DisplayName { get; init; } = displayName;
    public Coordinates Coordinates { get; init; } = coordinates;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}