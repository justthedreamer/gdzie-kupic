namespace Gdzie.Kupic.Storage.Entities.Marketplace;

using Gdzie.Kupic.Storage.Entities.Common;

public sealed class Merchant(
    Guid id,
    string name,
    string description,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt)
{
    public Guid Id { get; init; } = id;
    public string Name { get; init; } = name;
    public string Description { get; init; } = description;
    public BadDetails? BanDetails { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; init; } = updatedAt;

    public ICollection<MerchantAccount> MerchantAccounts { get; init; } = [];
    public ICollection<MerchantBranch> MerchantBranches { get; init; } = [];
    public ICollection<MerchantSubscription> Subscriptions { get; init; } = [];
}