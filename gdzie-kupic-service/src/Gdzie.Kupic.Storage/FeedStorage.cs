namespace Gdzie.Kupic.Storage;

using System.Text;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;

internal sealed class FeedStorage(AppDbContext db) : IFeedStorage
{
    // Distance is measured to the merchant's nearest branch.
    private const string BaseSql = """
        SELECT p."Id" AS "PostId",
               d."DistanceKm" AS "DistanceKm",
               (p."UrgentDeadline" IS NOT NULL) AS "IsUrgent",
               p."CreatedAt" AS "CreatedAt",
               r."State" AS "MyResponse"
        FROM "PostNotifications" n
        JOIN "Posts" p ON p."Id" = n."PostId"
        LEFT JOIN "MerchantResponses" r ON r."PostId" = p."Id" AND r."MerchantId" = n."MerchantId"
        CROSS JOIN LATERAL (
            SELECT COALESCE(MIN(ST_Distance(b."Coordinates", p."Coordinates")) / 1000.0, 0)::double precision AS "DistanceKm"
            FROM "MerchantBranches" b
            WHERE b."MerchantId" = n."MerchantId") d
        WHERE n."MerchantId" = {0}
        """;

    private sealed record FeedRow(Guid PostId, double DistanceKm, bool IsUrgent, DateTimeOffset CreatedAt, string? MyResponse);

    public async Task<FeedPage> GetFeedAsync(FeedQuery query, CancellationToken ct = default)
    {
        var rows = db.Database.IsNpgsql()
            ? await QueryPostGisAsync(query, ct)
            : await QueryInMemoryAsync(query, ct);

        var hasMore = rows.Count > query.Limit;

        return new FeedPage(await HydrateAsync(query.MerchantId, rows.Take(query.Limit).ToList(), ct), hasMore);
    }

    public async Task<FeedSummary> GetSummaryAsync(Guid merchantId, DateTimeOffset now, CancellationToken ct = default)
    {
        var open = db.PostNotifications
            .Where(n => n.MerchantId == merchantId && n.Post.Status == PostStatus.Active && n.Post.ExpiresAt > now);

        var responded = await open.CountAsync(
            n => db.MerchantResponses.Any(r => r.PostId == n.PostId && r.MerchantId == merchantId), ct);
        var total = await open.CountAsync(ct);

        return new FeedSummary(total - responded, responded);
    }

    public async Task<FeedEntry?> FindEntryAsync(Guid merchantId, Guid postId, CancellationToken ct = default)
    {
        if (!await db.PostNotifications.AnyAsync(n => n.MerchantId == merchantId && n.PostId == postId, ct))
            return null;

        var rows = db.Database.IsNpgsql()
            ? await db.Database
                .SqlQueryRaw<FeedRow>($"{BaseSql} AND p.\"Id\" = {{1}}", merchantId, postId)
                .ToListAsync(ct)
            : await QueryInMemoryAsync(
                new FeedQuery(merchantId, FeedTab.All, null, null, FeedSort.Newest, null, 1, DateTimeOffset.MinValue),
                ct, postId);

        return (await HydrateAsync(merchantId, rows, ct)).SingleOrDefault();
    }

    private async Task<List<FeedRow>> QueryPostGisAsync(FeedQuery q, CancellationToken ct)
    {
        var args = new List<object> { q.MerchantId };
        string Arg(object value)
        {
            args.Add(value);
            return $"{{{args.Count - 1}}}";
        }

        var sql = new StringBuilder(BaseSql);
        sql.Append($" AND p.\"Status\" = 'Active' AND p.\"ExpiresAt\" > {Arg(q.Now.UtcDateTime)}");

        switch (q.Tab)
        {
            case FeedTab.New: sql.Append(" AND r.\"Id\" IS NULL"); break;
            case FeedTab.Responded: sql.Append(" AND r.\"Id\" IS NOT NULL"); break;
        }

        if (q.CategoryId is { } categoryId) sql.Append($" AND p.\"CategoryId\" = {Arg(categoryId)}");
        if (q.MaxDistanceKm is { } maxKm) sql.Append($" AND d.\"DistanceKm\" <= {Arg(maxKm)}");

        if (q.Sort == FeedSort.Newest)
        {
            if (q.After is { } c)
            {
                sql.Append($" AND ((p.\"UrgentDeadline\" IS NOT NULL), p.\"CreatedAt\", p.\"Id\") < ({Arg(c.IsUrgent)}, {Arg(c.CreatedAt.UtcDateTime)}, {Arg(c.PostId)})");
            }

            sql.Append(" ORDER BY (p.\"UrgentDeadline\" IS NOT NULL) DESC, p.\"CreatedAt\" DESC, p.\"Id\" DESC");
        }
        else
        {
            if (q.After is { } c)
                sql.Append($" AND (d.\"DistanceKm\", p.\"Id\") > ({Arg(c.DistanceKm)}, {Arg(c.PostId)})");

            sql.Append(" ORDER BY d.\"DistanceKm\" ASC, p.\"Id\" ASC");
        }

        sql.Append($" LIMIT {Arg(q.Limit + 1)}");

        return await db.Database.SqlQueryRaw<FeedRow>(sql.ToString(), args.ToArray()).ToListAsync(ct);
    }

    // Test providers cannot run PostGIS SQL; the same rules are applied in memory.
    private async Task<List<FeedRow>> QueryInMemoryAsync(FeedQuery q, CancellationToken ct, Guid? onlyPostId = null)
    {
        var notified = db.PostNotifications.Where(n => n.MerchantId == q.MerchantId).Select(n => n.PostId);

        var posts = db.Posts.AsNoTracking().Where(p => notified.Contains(p.Id));
        if (onlyPostId is { } id) posts = posts.Where(p => p.Id == id);
        else posts = posts.Where(p => p.Status == PostStatus.Active && p.ExpiresAt > q.Now);

        var postList = await posts.ToListAsync(ct);
        var branches = await db.MerchantBranches.AsNoTracking().Where(b => b.MerchantId == q.MerchantId).ToListAsync(ct);
        var responses = await db.MerchantResponses.AsNoTracking().Where(r => r.MerchantId == q.MerchantId)
            .ToDictionaryAsync(r => r.PostId, r => r.State, ct);

        var rows = postList
            .Select(p => new FeedRow(
                p.Id,
                branches.Count == 0
                    ? 0
                    : branches.Min(b => MatchingRule.DistanceMeters(p.Coordinates, b.Coordinates)) / 1000,
                p.IsUrgent,
                p.CreatedAt,
                responses.TryGetValue(p.Id, out var state) ? state.ToString() : null));

        if (onlyPostId is not null) return rows.ToList();

        rows = q.Tab switch
        {
            FeedTab.New => rows.Where(r => r.MyResponse is null),
            FeedTab.Responded => rows.Where(r => r.MyResponse is not null),
            _ => rows,
        };

        if (q.CategoryId is { } categoryId)
        {
            var inCategory = postList.Where(p => p.CategoryId == categoryId).Select(p => p.Id).ToHashSet();
            rows = rows.Where(r => inCategory.Contains(r.PostId));
        }

        if (q.MaxDistanceKm is { } maxKm) rows = rows.Where(r => r.DistanceKm <= maxKm);

        if (q.Sort == FeedSort.Newest)
        {
            if (q.After is { } c)
            {
                rows = rows.Where(r => Compare(r, c) < 0);
            }

            return rows.OrderByDescending(r => r.IsUrgent).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.PostId)
                .Take(q.Limit + 1).ToList();
        }

        if (q.After is { } after)
        {
            rows = rows.Where(r => r.DistanceKm > after.DistanceKm
                                   || (r.DistanceKm == after.DistanceKm && r.PostId.CompareTo(after.PostId) > 0));
        }

        return rows.OrderBy(r => r.DistanceKm).ThenBy(r => r.PostId).Take(q.Limit + 1).ToList();
    }

    private static int Compare(FeedRow r, FeedCursor c)
    {
        var byUrgency = r.IsUrgent.CompareTo(c.IsUrgent);
        if (byUrgency != 0) return byUrgency;

        var byTime = r.CreatedAt.CompareTo(c.CreatedAt);
        return byTime != 0 ? byTime : r.PostId.CompareTo(c.PostId);
    }

    private async Task<List<FeedEntry>> HydrateAsync(Guid merchantId, List<FeedRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.PostId).ToList();

        var posts = await db.Posts.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Tag)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var threads = await db.ChatThreads.AsNoTracking()
            .Where(t => t.MerchantId == merchantId && ids.Contains(t.PostId))
            .ToDictionaryAsync(t => t.PostId, t => t.Id, ct);

        // Only the first name is read from the users table (never the e-mail address).
        var buyerIds = posts.Values.Select(p => p.BuyerId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => buyerIds.Contains(u.Id) && u.FirstName != null)
            .ToDictionaryAsync(u => u.Id, u => u.FirstName, ct);

        return rows
            .Select(r => new FeedEntry(
                posts[r.PostId],
                r.DistanceKm,
                r.MyResponse is null ? null : Enum.Parse<ResponseState>(r.MyResponse),
                threads.TryGetValue(r.PostId, out var threadId) ? threadId : null,
                names.TryGetValue(posts[r.PostId].BuyerId, out var name) ? name : null))
            .ToList();
    }
}