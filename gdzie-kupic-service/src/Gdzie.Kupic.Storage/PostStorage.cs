namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Infrastructure;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;

internal sealed class PostStorage(AppDbContext db) : IPostStorage
{
    private const int ExpireBatchSize = 500;

    public async Task AddWithOutboxAsync(Post post, OutboxMessage outboxMessage, CancellationToken ct = default)
    {
        db.Posts.Add(post);
        db.Outbox.Add(outboxMessage);
        await db.SaveChangesAsync(ct);
    }

    public Task<Post?> FindBuyerPostAsync(Guid postId, Guid buyerId, CancellationToken ct = default) =>
        db.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Tag)
            .SingleOrDefaultAsync(p => p.Id == postId && p.BuyerId == buyerId, ct);

    public Task<Post?> FindBuyerPostForUpdateAsync(Guid postId, Guid buyerId, CancellationToken ct = default) =>
        db.Posts.SingleOrDefaultAsync(p => p.Id == postId && p.BuyerId == buyerId, ct);

    public Task<Post?> FindForUpdateAsync(Guid postId, CancellationToken ct = default) =>
        db.Posts.SingleOrDefaultAsync(p => p.Id == postId, ct);

    public async Task<IReadOnlyDictionary<Guid, int>> GetNotifiedCountsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken ct = default) =>
        await db.PostNotifications
            .Where(n => postIds.Contains(n.PostId))
            .GroupBy(n => n.PostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PostId, x => x.Count, ct);

    public async Task<IReadOnlyList<Post>> ListBuyerPostsAsync(Guid buyerId, bool active, int limit, CancellationToken ct = default)
    {
        var query = db.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Tag)
            .Where(p => p.BuyerId == buyerId);

        query = active
            ? query.Where(p => p.Status == PostStatus.Active)
            : query.Where(p => p.Status != PostStatus.Active);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> ExpireOverduePostsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var total = 0;

        while (true)
        {
            var batch = await db.Posts
                .Where(p => p.Status == PostStatus.Active && p.ExpiresAt <= now)
                .OrderBy(p => p.ExpiresAt)
                .Take(ExpireBatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) return total;

            foreach (var post in batch) post.TryExpire(now);

            await db.SaveChangesAsync(ct);
            total += batch.Count;
            db.ChangeTracker.Clear();
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
