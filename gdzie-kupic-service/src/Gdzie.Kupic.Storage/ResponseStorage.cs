namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;

internal sealed class ResponseStorage(AppDbContext db) : IResponseStorage
{
    private const int MaxAttempts = 3;

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