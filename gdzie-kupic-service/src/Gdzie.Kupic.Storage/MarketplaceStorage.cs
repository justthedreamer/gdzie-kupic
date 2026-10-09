namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;

internal sealed class MarketplaceStorage(AppDbContext db) : IMarketplaceStorage
{
    public Task<Merchant?> FindMerchantByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Merchants
            .AsNoTracking()
            .Include(m => m.Branches)
            .Where(m => m.Accounts.Any(a => a.UserId == userId))
            .SingleOrDefaultAsync(ct);

    public async Task<bool> TryAddOnboardingAsync(
        Merchant merchant,
        MerchantAccount account,
        MerchantBranch branch,
        CancellationToken ct = default)
    {
        if (await db.MerchantAccounts.AnyAsync(a => a.UserId == account.UserId, ct)) return false;

        db.Merchants.Add(merchant);
        db.MerchantAccounts.Add(account);
        db.MerchantBranches.Add(branch);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost a race against a concurrent onboarding of the same user (unique index on UserId).
            db.ChangeTracker.Clear();
            if (await db.MerchantAccounts.AnyAsync(a => a.UserId == account.UserId, ct)) return false;
            throw;
        }
    }

    public async Task<Guid?> FindMerchantIdByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await db.MerchantAccounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => (Guid?)a.MerchantId)
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<MerchantSubscription>> GetSubscriptionsAsync(Guid merchantId, CancellationToken ct = default) =>
        await db.MerchantSubscriptions
            .AsNoTracking()
            .Where(s => s.MerchantId == merchantId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<bool> TryAddSubscriptionAsync(MerchantSubscription subscription, CancellationToken ct = default)
    {
        if (await SubscriptionExistsAsync(subscription, ct)) return false;

        db.MerchantSubscriptions.Add(subscription);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost a race against a concurrent identical subscription (unique indexes).
            db.ChangeTracker.Clear();
            if (await SubscriptionExistsAsync(subscription, ct)) return false;
            throw;
        }
    }

    public async Task<bool> DeleteSubscriptionAsync(Guid merchantId, Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await db.MerchantSubscriptions
            .SingleOrDefaultAsync(s => s.Id == subscriptionId && s.MerchantId == merchantId, ct);
        if (subscription is null) return false;

        db.MerchantSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> SubscriptionExistsAsync(MerchantSubscription s, CancellationToken ct) =>
        db.MerchantSubscriptions.AnyAsync(
            x => x.MerchantId == s.MerchantId && x.CategoryId == s.CategoryId && x.TagId == s.TagId, ct);
}