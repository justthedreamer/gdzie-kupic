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

    public async Task<IReadOnlyList<Guid>> AddNotificationsAsync(
        Guid postId, IReadOnlyCollection<Guid> merchantIds, DateTimeOffset now, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var existing = await db.PostNotifications
                .Where(n => n.PostId == postId && merchantIds.Contains(n.MerchantId))
                .Select(n => n.MerchantId)
                .ToListAsync(ct);

            var created = merchantIds.Distinct().Except(existing).ToList();
            if (created.Count == 0) return created;

            db.PostNotifications.AddRange(created.Select(id => new PostNotification(Guid.NewGuid(), postId, id, now)));

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
