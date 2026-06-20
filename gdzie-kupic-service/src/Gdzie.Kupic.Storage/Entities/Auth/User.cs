namespace Gdzie.Kupic.Storage.Entities.Auth;

using Gdzie.Kupic.Storage.Entities.Common;
using Gdzie.Kupic.Storage.Entities.Marketplace;
using Gdzie.Kupic.Storage.Entities.Notifications;

public sealed class User(
    Guid id,
    string email,
    string? passwordHash,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public string Email { get; init; } = email;
    public string? PasswordHash { get; init; } = passwordHash;
    public BadDetails? BanDetails { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public BuyerProfile? BuyerProfile { get; init; }
    public MerchantAccount? MerchantAccount { get; init; }

    public ICollection<RefreshToken> RefreshTokens { get; init; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; init; } = [];
    public ICollection<PushSubscription> PushSubscriptions { get; init; } = [];
}