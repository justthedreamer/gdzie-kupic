namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Microsoft.EntityFrameworkCore;

internal sealed class MatchingStorage(AppDbContext db) : IMatchingStorage
{
    public async Task<IReadOnlyList<Guid>> FindMatchingMerchantIdsAsync(PostMatchCriteria c, CancellationToken ct = default)
    {
        if (db.Database.IsNpgsql()) return await FindWithPostGisAsync(c, ct);

        // Non-PostGIS providers (in-memory tests) cannot run spatial SQL, so distance is evaluated in .NET.
        var candidates = await db.Merchants
            .AsNoTracking()
            .Include(m => m.Branches)
            .Where(m => m.BanDetails == null)
            .Where(m => !m.Accounts.Any(a => a.UserId == c.BuyerId))
            .Where(m => db.MerchantSubscriptions.Any(s =>
                s.MerchantId == m.Id && s.CategoryId == c.CategoryId && (s.TagId == null || s.TagId == c.TagId)))
            .ToListAsync(ct);

        return candidates
            .Where(m => m.Branches.Any(b => MatchingRule.IsWithinReach(c.Coordinates, c.RadiusKm, b.Coordinates)))
            .Select(m => m.Id)
            .Order()
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> FindMatchingOpenPostIdsAsync(Guid merchantId, DateTimeOffset now, CancellationToken ct = default)
    {
        if (db.Database.IsNpgsql()) return await FindPostsWithPostGisAsync(merchantId, now, ct);

        var merchant = await db.Merchants
            .AsNoTracking()
            .Include(m => m.Branches)
            .Include(m => m.Accounts)
            .SingleOrDefaultAsync(m => m.Id == merchantId && m.BanDetails == null, ct);
        if (merchant is null) return [];

        var subscriptions = await db.MerchantSubscriptions.AsNoTracking().Where(s => s.MerchantId == merchantId).ToListAsync(ct);
        var ownUserIds = merchant.Accounts.Select(a => a.UserId).ToList();
        var alreadyNotified = await db.PostNotifications.Where(n => n.MerchantId == merchantId).Select(n => n.PostId).ToListAsync(ct);

        var openPosts = await db.Posts
            .AsNoTracking()
            .Where(p => p.Status == PostStatus.Active && p.ExpiresAt > now)
            .ToListAsync(ct);

        return openPosts
            .Where(p => !ownUserIds.Contains(p.BuyerId))
            .Where(p => !alreadyNotified.Contains(p.Id))
            .Where(p => subscriptions.Any(s => MatchingRule.IsSubscriptionMatch(s, p.CategoryId, p.TagId)))
            .Where(p => merchant.Branches.Any(b => MatchingRule.IsWithinReach(p.Coordinates, p.RadiusKm, b.Coordinates)))
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.Id)
            .ToList();
    }

    public Task<IReadOnlyList<Guid>> AddMerchantNotificationsAsync(
        Guid merchantId, IReadOnlyCollection<Guid> postIds, DateTimeOffset now, CancellationToken ct = default) =>
        AddNotificationsCoreAsync(
            postIds,
            async () => await db.PostNotifications
                .Where(n => n.MerchantId == merchantId && postIds.Contains(n.PostId))
                .Select(n => n.PostId)
                .ToListAsync(ct),
            postId => new PostNotification(Guid.NewGuid(), postId, merchantId, now),
            ct);

    public Task<IReadOnlyList<Guid>> AddNotificationsAsync(
        Guid postId, IReadOnlyCollection<Guid> merchantIds, DateTimeOffset now, CancellationToken ct = default) =>
        AddNotificationsCoreAsync(
            merchantIds,
            async () => await db.PostNotifications
                .Where(n => n.PostId == postId && merchantIds.Contains(n.MerchantId))
                .Select(n => n.MerchantId)
                .ToListAsync(ct),
            merchantId => new PostNotification(Guid.NewGuid(), postId, merchantId, now),
            ct);

    private async Task<IReadOnlyList<Guid>> AddNotificationsCoreAsync(
        IReadOnlyCollection<Guid> keys,
        Func<Task<List<Guid>>> loadExisting,
        Func<Guid, PostNotification> create,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var existing = await loadExisting();
            var created = keys.Distinct().Except(existing).ToList();
            if (created.Count == 0) return created;

            db.PostNotifications.AddRange(created.Select(create));

            try
            {
                await db.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A concurrent run recorded some of them first (unique index); re-read and retry once.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> FindPostsWithPostGisAsync(Guid merchantId, DateTimeOffset now, CancellationToken ct) =>
        await db.Database
            .SqlQuery<Guid>($"""
                             SELECT p."Id" AS "Value"
                             FROM "Posts" p
                             WHERE p."Status" = 'Active'
                               AND p."ExpiresAt" > {now}
                               AND EXISTS (
                                   SELECT 1 FROM "Merchants" m WHERE m."Id" = {merchantId} AND m."BannedAt" IS NULL)
                               AND NOT EXISTS (
                                   SELECT 1 FROM "MerchantAccounts" a
                                   WHERE a."MerchantId" = {merchantId} AND a."UserId" = p."BuyerId")
                               AND EXISTS (
                                   SELECT 1 FROM "MerchantSubscriptions" s
                                   WHERE s."MerchantId" = {merchantId}
                                     AND s."CategoryId" = p."CategoryId"
                                     AND (s."TagId" IS NULL OR s."TagId" = p."TagId"))
                               AND NOT EXISTS (
                                   SELECT 1 FROM "PostNotifications" n
                                   WHERE n."PostId" = p."Id" AND n."MerchantId" = {merchantId})
                               AND (p."RadiusKm" IS NULL OR EXISTS (
                                   SELECT 1 FROM "MerchantBranches" b
                                   WHERE b."MerchantId" = {merchantId}
                                     AND ST_DWithin(b."Coordinates", p."Coordinates",
                                                    ((p."RadiusKm" + {MatchingRule.ToleranceKm}) * 1000)::double precision)))
                             ORDER BY p."CreatedAt"
                             """)
            .ToListAsync(ct);

    private async Task<IReadOnlyList<Guid>> FindWithPostGisAsync(PostMatchCriteria c, CancellationToken ct)
    {
        var unlimited = c.RadiusKm is null;
        var maxMeters = MatchingRule.MaxDistanceMeters(c.RadiusKm) ?? 0;
        var longitude = c.Coordinates.Longitude;
        var latitude = c.Coordinates.Latitude;

        return await db.Database
            .SqlQuery<Guid>($"""
                             SELECT DISTINCT m."Id" AS "Value"
                             FROM "Merchants" m
                             JOIN "MerchantSubscriptions" s ON s."MerchantId" = m."Id"
                             WHERE m."BannedAt" IS NULL
                               AND s."CategoryId" = {c.CategoryId}
                               AND (s."TagId" IS NULL OR s."TagId" = {c.TagId})
                               AND NOT EXISTS (
                                   SELECT 1 FROM "MerchantAccounts" a
                                   WHERE a."MerchantId" = m."Id" AND a."UserId" = {c.BuyerId})
                               AND ({unlimited} OR EXISTS (
                                   SELECT 1 FROM "MerchantBranches" b
                                   WHERE b."MerchantId" = m."Id"
                                     AND ST_DWithin(b."Coordinates",
                                                    ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography,
                                                    {maxMeters})))
                             ORDER BY 1
                             """)
            .ToListAsync(ct);
    }
}
