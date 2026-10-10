namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;

public enum ChatSide
{
    Buyer,
    Merchant,
}

/// <summary>The caller of a chat operation; <see cref="MerchantId"/> is set for the merchant side.</summary>
public sealed record ChatActor(Guid UserId, ChatSide Side, Guid? MerchantId);

public sealed record ChatThreadInfo(
    Guid Id,
    Guid PostId,
    string PostTitle,
    PostStatus PostStatus,
    Guid BuyerId,
    Guid MerchantId,
    string? MerchantName,
    string? LastBody,
    Guid? LastSenderId,
    DateTimeOffset? LastMessageAt,
    int UnreadCount,
    bool IsLocked,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);

public sealed record ChatThreadCursor(DateTimeOffset LastActivityAt, Guid ThreadId);

public sealed record ChatThreadPage(IReadOnlyList<ChatThreadInfo> Items, bool HasMore);

public sealed record ChatMessagePage(IReadOnlyList<ChatMessage> Items, bool HasMore);

public interface IChatStorage
{
    /// <summary>The thread, or null when it does not exist or the actor is not one of its participants.</summary>
    Task<ChatThreadInfo?> FindThreadAsync(ChatActor actor, Guid threadId, CancellationToken ct = default);

    /// <summary>The actor's threads, newest activity first.</summary>
    Task<ChatThreadPage> ListThreadsAsync(ChatActor actor, ChatThreadCursor? after, int limit, CancellationToken ct = default);

    /// <summary>
    /// A page of messages in ascending order. Without a cursor it is the latest page; <paramref name="before"/> pages
    /// towards older messages and <paramref name="after"/> towards newer ones. Returns null when a cursor message does
    /// not belong to the thread.
    /// </summary>
    Task<ChatMessagePage?> GetMessagesAsync(
        Guid threadId, Guid? before, Guid? after, int limit, CancellationToken ct = default);

    Task AddMessageAsync(ChatMessage message, CancellationToken ct = default);

    Task MarkReadAsync(ChatActor actor, Guid threadId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Number of unread counterpart messages across all of the actor's threads.</summary>
    Task<int> GetUnreadCountAsync(ChatActor actor, CancellationToken ct = default);

    /// <summary>
    /// Locks every thread the user takes part in (<paramref name="banned"/> = true) or, when the user is unbanned,
    /// recomputes the lock: a thread stays locked while any of its participants is still banned. Called by the ban
    /// cascade inside the ban/unban transaction. Returns the number of threads whose flag changed.
    /// </summary>
    Task<int> SetLockForUserAsync(Guid userId, bool banned, CancellationToken ct = default);
}