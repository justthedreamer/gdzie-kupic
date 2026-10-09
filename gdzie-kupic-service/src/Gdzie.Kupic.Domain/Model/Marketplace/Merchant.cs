namespace Gdzie.Kupic.Domain.Model.Marketplace;

using Gdzie.Kupic.Domain.Model.Common;

public sealed class Merchant(
    Guid id,
    string name,
    string? description,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public string Name { get; set; } = name;
    public string? Description { get; set; } = description;
    public BanDetails? BanDetails { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; set; } = createdAt;

    public ICollection<MerchantAccount> Accounts { get; init; } = [];
    public ICollection<MerchantBranch> Branches { get; init; } = [];
}