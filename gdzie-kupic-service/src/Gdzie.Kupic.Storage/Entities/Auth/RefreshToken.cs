namespace Gdzie.Kupic.Storage.Entities.Auth;

public sealed class RefreshToken(
    Guid id,
    Guid userId,
    string tokenHash,
    DateTimeOffset expiresAt,
    DateTimeOffset? revokedAt,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string TokenHash { get; init; } = tokenHash;
    public DateTimeOffset ExpiresAt { get; init; } = expiresAt;
    public DateTimeOffset? RevokedAt { get; init; } = revokedAt;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public User User { get; init; } = null!;
}
