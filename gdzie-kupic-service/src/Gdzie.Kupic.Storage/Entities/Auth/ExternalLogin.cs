namespace Gdzie.Kupic.Storage.Entities.Auth;

public enum OAuthProvider
{
    Google,
}

public sealed class ExternalLogin(
    Guid id,
    Guid buyerProfileId,
    OAuthProvider provider,
    string providerUserId,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid BuyerProfileId { get; init; } = buyerProfileId;
    public OAuthProvider Provider { get; init; } = provider;
    public string ProviderUserId { get; init; } = providerUserId;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public BuyerProfile BuyerProfile { get; init; } = null!;
}
