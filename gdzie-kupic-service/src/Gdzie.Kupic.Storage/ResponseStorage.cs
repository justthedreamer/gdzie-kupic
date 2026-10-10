namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;

internal sealed class ResponseStorage(AppDbContext db) : IResponseStorage
{
    private const int MaxAttempts = 3;

    public async Task<IReadOnlyDictionary<ResponseState, int>> CountByStateAsync(Guid postId, CancellationToken ct = default) =>
        (await db.MerchantResponses.AsNoTracking()
            .Where(r => r.PostId == postId)
            .GroupBy(r => r.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct))
        .ToDictionary(x => x.State, x => x.Count);

    public async Task<IReadOnlyList<PostResponseInfo>> ListPositiveAsync(Guid postId, Guid buyerId, CancellationToken ct = default)
    {
        var responses = await db.MerchantResponses.AsNoTracking()
            .Where(r => r.PostId == postId && r.State != ResponseState.CantHelp)
            .Select(r => new
            {
                r.MerchantId,
                r.State,
                r.UpdatedAt,
                ShopName = db.Merchants.Where(m => m.Id == r.MerchantId).Select(m => m.Name).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var threads = await db.ChatThreads.AsNoTracking()
            .Where(t => t.PostId == postId)
            .Select(t => new
            {
                t.Id,
                t.MerchantId,
                Unread = t.Messages.Count(m => m.SenderId != buyerId && (t.BuyerLastReadAt == null || m.CreatedAt > t.BuyerLastReadAt)),
            })
            .ToListAsync(ct);
        var byMerchant = threads.ToDictionary(t => t.MerchantId);

        return responses
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r =>
            {
                byMerchant.TryGetValue(r.MerchantId, out var thread);
                return new PostResponseInfo(r.MerchantId, r.ShopName ?? string.Empty, r.State, thread?.Id, thread?.Unread ?? 0, r.UpdatedAt);
            })
            .ToList();
    }
    public async Task<ResponseSaveResult> SaveAsync(
        Guid postId, Guid merchantId, ResponseState state, DateTimeOffset now, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TrySaveAsync(postId, merchantId, state, now, ct);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Lost a race on the unique (PostId, MerchantId) indexes; the retry sees the winner's rows.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<ResponseSaveResult> TrySaveAsync(
        Guid postId, Guid merchantId, ResponseState state, DateTimeOffset now, CancellationToken ct)
    {
        // Transactions and row locks are PostgreSQL-specific; other providers (tests) run single-threaded.
        var npgsql = db.Database.IsNpgsql();
        await using var transaction = npgsql ? await db.Database.BeginTransactionAsync(ct) : null;

        var result = await WriteAsync(postId, merchantId, state, now, npgsql, ct);

        if (transaction is not null) await transaction.CommitAsync(ct);

        return result;
    }

    private async Task<ResponseSaveResult> WriteAsync(
        Guid postId, Guid merchantId, ResponseState state, DateTimeOffset now, bool lockPost, CancellationToken ct)
    {
        var notified = await db.PostNotifications
            .AnyAsync(n => n.PostId == postId && n.MerchantId == merchantId, ct);
        if (!notified) return new ResponseSaveResult(ResponseSaveStatus.NotFound);

        var post = lockPost
            ? (await db.Posts
                .FromSql($"""SELECT * FROM "Posts" WHERE "Id" = {postId} FOR SHARE""")
                .ToListAsync(ct)).SingleOrDefault()
            : await db.Posts.SingleOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return new ResponseSaveResult(ResponseSaveStatus.NotFound);

        var response = await db.MerchantResponses
            .SingleOrDefaultAsync(r => r.PostId == postId && r.MerchantId == merchantId, ct);

        if (response is null)
        {
            if (!post.IsOpen(now)) return new ResponseSaveResult(ResponseSaveStatus.PostNotActive);

            response = new MerchantResponse(Guid.NewGuid(), postId, merchantId, state, now);
            db.MerchantResponses.Add(response);
        }
        else if (!response.TryChangeState(state, post, now))
        {
            return new ResponseSaveResult(ResponseSaveStatus.PostNotActive);
        }

        var threadId = await db.ChatThreads
            .Where(t => t.PostId == postId && t.MerchantId == merchantId)
            .Select(t => (Guid?)t.Id)
            .SingleOrDefaultAsync(ct);

        if (threadId is null && state.IsPositive())
        {
            var thread = new ChatThread(Guid.NewGuid(), postId, merchantId, false, now);
            db.ChatThreads.Add(thread);
            threadId = thread.Id;
        }

        await db.SaveChangesAsync(ct);

        return new ResponseSaveResult(ResponseSaveStatus.Saved, response, threadId);
    }
}