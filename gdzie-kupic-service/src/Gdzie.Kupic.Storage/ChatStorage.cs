namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;

internal sealed class ChatStorage(AppDbContext db) : IChatStorage
{
    public async Task<ChatThreadInfo?> FindThreadAsync(ChatActor actor, Guid threadId, CancellationToken ct = default) =>
        (await Project(Participating(actor).Where(t => t.Id == threadId), actor).ToListAsync(ct)).SingleOrDefault()?.ToInfo();

    public async Task<ChatThreadPage> ListThreadsAsync(
        ChatActor actor, ChatThreadCursor? after, int limit, CancellationToken ct = default)
    {
        var query = Project(Participating(actor), actor);

        if (after is { } c)
        {
            query = query.Where(i =>
                i.LastActivityAt < c.LastActivityAt
                || (i.LastActivityAt == c.LastActivityAt && i.Id.CompareTo(c.ThreadId) < 0));
        }

        var rows = await query
            .OrderByDescending(i => i.LastActivityAt)
            .ThenByDescending(i => i.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        return new ChatThreadPage(rows.Take(limit).Select(r => r.ToInfo()).ToList(), rows.Count > limit);
    }

    public async Task<ChatMessagePage?> GetMessagesAsync(
        Guid threadId, Guid? before, Guid? after, int limit, CancellationToken ct = default)
    {
        var messages = db.ChatMessages.AsNoTracking().Where(m => m.ThreadId == threadId);

        var cursorId = before ?? after;
        if (cursorId is { } id)
        {
            var cursorAt = await messages.Where(m => m.Id == id).Select(m => (DateTimeOffset?)m.CreatedAt).SingleOrDefaultAsync(ct);
            if (cursorAt is not { } at) return null;

            messages = before is not null
                ? messages.Where(m => m.CreatedAt < at || (m.CreatedAt == at && m.Id.CompareTo(id) < 0))
                : messages.Where(m => m.CreatedAt > at || (m.CreatedAt == at && m.Id.CompareTo(id) > 0));
        }

        // Without a cursor and with `before` the newest messages of the range are wanted; `after` takes the oldest.
        var ascending = after is not null;
        var ordered = ascending
            ? messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            : messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id);

        var rows = await ordered.Take(limit + 1).ToListAsync(ct);
        var page = rows.Take(limit).ToList();
        if (!ascending) page.Reverse();

        return new ChatMessagePage(page, rows.Count > limit);
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkReadAsync(ChatActor actor, Guid threadId, DateTimeOffset now, CancellationToken ct = default)
    {
        var thread = await Participating(actor, tracked: true).SingleOrDefaultAsync(t => t.Id == threadId, ct);
        if (thread is null) return;

        if (actor.Side == ChatSide.Buyer) thread.BuyerLastReadAt = now;
        else thread.MerchantLastReadAt = now;

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(ChatActor actor, CancellationToken ct = default)
    {
        var threads = await Project(Participating(actor), actor).Select(i => i.UnreadCount).ToListAsync(ct);

        return threads.Sum();
    }

    public async Task<IReadOnlyList<Guid>> SetLockForUserAsync(Guid userId, bool banned, CancellationToken ct = default)
    {
        var merchantIds = db.MerchantAccounts.Where(a => a.UserId == userId).Select(a => a.MerchantId);

        var threads = await db.ChatThreads
            .Where(t => t.Post.BuyerId == userId || merchantIds.Contains(t.MerchantId))
            .ToListAsync(ct);

        var changed = new List<Guid>();
        foreach (var thread in threads)
        {
            var locked = banned || await HasBannedParticipantAsync(thread, ct);
            if (thread.IsLocked == locked) continue;

            thread.IsLocked = locked;
            changed.Add(thread.Id);
        }

        await db.SaveChangesAsync(ct);

        return changed;
    }

    public async Task<bool> StartsUnreadSeriesForBuyerAsync(Guid threadId, Guid messageId, CancellationToken ct = default)
    {
        var thread = await db.ChatThreads.AsNoTracking()
            .Where(t => t.Id == threadId)
            .Select(t => new { t.Post.BuyerId, t.BuyerLastReadAt })
            .SingleOrDefaultAsync(ct);
        var message = await db.ChatMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == messageId, ct);
        if (thread is null || message is null) return false;

        var readAt = thread.BuyerLastReadAt ?? DateTimeOffset.MinValue;

        return !await db.ChatMessages.AsNoTracking().AnyAsync(m =>
            m.ThreadId == threadId && m.SenderId != thread.BuyerId && m.Id != messageId
            && m.CreatedAt > readAt && m.CreatedAt <= message.CreatedAt, ct);
    }

    public async Task<ChatThreadParticipants?> FindParticipantsAsync(Guid threadId, CancellationToken ct = default)
    {
        var thread = await db.ChatThreads.AsNoTracking()
            .Where(t => t.Id == threadId)
            .Select(t => new { t.PostId, t.Post.BuyerId, t.MerchantId })
            .SingleOrDefaultAsync(ct);
        if (thread is null) return null;

        var merchantUserIds = await db.MerchantAccounts.AsNoTracking()
            .Where(a => a.MerchantId == thread.MerchantId)
            .Select(a => a.UserId)
            .ToListAsync(ct);

        return new ChatThreadParticipants(thread.PostId, thread.BuyerId, merchantUserIds);
    }

    private async Task<bool> HasBannedParticipantAsync(ChatThread thread, CancellationToken ct)
    {
        var buyerId = await db.Posts.Where(p => p.Id == thread.PostId).Select(p => p.BuyerId).SingleAsync(ct);
        if (await db.Users.AnyAsync(u => u.Id == buyerId && u.BanDetails != null, ct)) return true;

        if (await db.Merchants.AnyAsync(m => m.Id == thread.MerchantId && m.BanDetails != null, ct)) return true;

        return await db.MerchantAccounts
            .Where(a => a.MerchantId == thread.MerchantId)
            .AnyAsync(a => db.Users.Any(u => u.Id == a.UserId && u.BanDetails != null), ct);
    }

    private IQueryable<ChatThread> Participating(ChatActor actor, bool tracked = false)
    {
        var threads = tracked ? db.ChatThreads : db.ChatThreads.AsNoTracking();

        return actor.Side == ChatSide.Buyer
            ? threads.Where(t => t.Post.BuyerId == actor.UserId)
            : threads.Where(t => t.MerchantId == actor.MerchantId);
    }

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid PostId { get; init; }
        public string PostTitle { get; init; } = "";
        public PostStatus PostStatus { get; init; }
        public Guid BuyerId { get; init; }
        public Guid MerchantId { get; init; }
        public string? MerchantName { get; init; }
        public string? BuyerFirstName { get; init; }
        public string? LastBody { get; init; }
        public Guid? LastSenderId { get; init; }
        public DateTimeOffset? LastMessageAt { get; init; }
        public int UnreadCount { get; init; }
        public bool IsLocked { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset LastActivityAt { get; init; }

        public ChatThreadInfo ToInfo() => new(Id, PostId, PostTitle, PostStatus, BuyerId, MerchantId, MerchantName,
            LastBody, LastSenderId, LastMessageAt, UnreadCount, IsLocked, CreatedAt, LastActivityAt, BuyerFirstName);
    }

    private IQueryable<Row> Project(IQueryable<ChatThread> threads, ChatActor actor)
    {
        var isBuyer = actor.Side == ChatSide.Buyer;

        return threads.Select(t => new Row
        {
            Id = t.Id,
            PostId = t.PostId,
            PostTitle = t.Post.Title,
            PostStatus = t.Post.Status,
            BuyerId = t.Post.BuyerId,
            MerchantId = t.MerchantId,
            MerchantName = db.Merchants.Where(m => m.Id == t.MerchantId).Select(m => m.Name).FirstOrDefault(),
            BuyerFirstName = db.Users.Where(u => u.Id == t.Post.BuyerId).Select(u => u.FirstName).FirstOrDefault(),
            LastBody = t.Messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Select(m => m.Body).FirstOrDefault(),
            LastSenderId = t.Messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Select(m => (Guid?)m.SenderId).FirstOrDefault(),
            LastMessageAt = t.Messages.Max(m => (DateTimeOffset?)m.CreatedAt),
            // Counterpart messages newer than the caller's read marker.
            UnreadCount = isBuyer
                ? t.Messages.Count(m => m.SenderId != t.Post.BuyerId && (t.BuyerLastReadAt == null || m.CreatedAt > t.BuyerLastReadAt))
                : t.Messages.Count(m => m.SenderId == t.Post.BuyerId && (t.MerchantLastReadAt == null || m.CreatedAt > t.MerchantLastReadAt)),
            IsLocked = t.IsLocked,
            CreatedAt = t.CreatedAt,
            LastActivityAt = t.Messages.Max(m => (DateTimeOffset?)m.CreatedAt) ?? t.CreatedAt,
        });
    }
}