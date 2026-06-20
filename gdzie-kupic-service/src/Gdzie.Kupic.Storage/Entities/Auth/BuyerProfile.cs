namespace Gdzie.Kupic.Storage.Entities.Auth;

using Gdzie.Kupic.Storage.Entities.Location;
using Gdzie.Kupic.Storage.Entities.Marketplace;

public sealed class BuyerProfile(
    Guid userId,
    string displayName,
    DateTimeOffset createdAt)
{
    public Guid UserId { get; init; } = userId;
    public string DisplayName { get; init; } = displayName;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public User User { get; init; } = null!;
    public ICollection<ExternalLogin> ExternalLogins { get; init; } = [];
    public ICollection<SavedLocation> SavedLocations { get; init; } = [];
    public ICollection<Post> Posts { get; init; } = [];
}
