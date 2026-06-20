namespace Gdzie.Kupic.Storage.Entities.Auth;

public sealed class PasswordResetToken(
    Guid id,
    Guid userId,
    string tokenHash,
    DateTimeOffset expiresAt,
    DateTimeOffset? usedAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string TokenHash { get; init; } = tokenHash;
    public DateTimeOffset ExpiresAt { get; init; } = expiresAt;
    public DateTimeOffset? UsedAt { get; init; } = usedAt;

    public User User { get; init; } = null!;
}
