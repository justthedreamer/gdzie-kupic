namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Infrastructure;
using Gdzie.Kupic.Domain.Model.Marketplace;

public interface IMarketplaceStorage
{
    /// <summary>The merchant (with branches) the user belongs to, or null if the user has not onboarded.</summary>
    Task<Merchant?> FindMerchantByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Persists merchant, account link, branch and the outbox entry in a single save (one transaction). Returns false
    /// when the user already belongs to a merchant (unique violation) and nothing was stored.
    /// </summary>
    Task<bool> TryAddOnboardingAsync(
        Merchant merchant,
        MerchantAccount account,
        MerchantBranch branch,
        OutboxMessage outboxMessage,
        CancellationToken ct = default);

    Task<Guid?> FindMerchantIdByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<MerchantSubscription>> GetSubscriptionsAsync(Guid merchantId, CancellationToken ct = default);

    /// <summary>Stores the subscription and the outbox entry together; returns false when the same (category, tag) subscription already exists for the merchant.</summary>
    Task<bool> TryAddSubscriptionAsync(MerchantSubscription subscription, OutboxMessage outboxMessage, CancellationToken ct = default);

    /// <summary>Removes the subscription only if it belongs to the merchant; returns false otherwise.</summary>
    Task<bool> DeleteSubscriptionAsync(Guid merchantId, Guid subscriptionId, CancellationToken ct = default);
}