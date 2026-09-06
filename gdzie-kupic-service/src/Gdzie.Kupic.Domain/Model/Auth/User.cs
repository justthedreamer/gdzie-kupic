namespace Gdzie.Kupic.Domain.Model.Auth;

using Gdzie.Kupic.Domain.Model.Common;

public sealed class User(
    Guid id,
    string email,
    string? passwordHash,
    Role role,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public string Email { get; init; } = email;
    public string? PasswordHash { get; init; } = passwordHash;
    public Role Role { get; init; } = role;
    public BanDetails? BanDetails { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public ICollection<RefreshToken> RefreshTokens { get; init; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; init; } = [];

    // TODO: Re-add PushSubscriptions navigation once the Notifications module is configured (Phase 3+).
}
