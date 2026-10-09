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
}