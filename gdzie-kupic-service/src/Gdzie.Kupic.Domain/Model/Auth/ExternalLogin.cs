namespace Gdzie.Kupic.Domain.Model.Auth;

public sealed class ExternalLogin(
    Guid id,
    Guid userId,
    string provider,
    string providerKey,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string Provider { get; init; } = provider;
    public string ProviderKey { get; init; } = providerKey;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public User User { get; init; } = null!;
}
