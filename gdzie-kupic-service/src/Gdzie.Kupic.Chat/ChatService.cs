namespace Gdzie.Kupic.Chat;

using System.Text;
using System.Text.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Storage;

internal sealed class ChatService(
    IChatStorage chat,
    IMarketplaceStorage marketplace,
    TimeProvider clock) : IChatService
{
    public const int MaxBodyLength = 2000;
    public const int DefaultThreadsLimit = 20;
    public const int MaxThreadsLimit = 50;
    public const int DefaultMessagesLimit = 30;
    public const int MaxMessagesLimit = 100;

    // The user model has no first name yet (see the first-name follow-up); shown to merchants until it does.
    public const string BuyerNamePlaceholder = "Kupuj\u0105cy";

    public async Task<ChatResult<ChatThreadsResult>> ListThreadsAsync(
        Guid userId, Role role, string? cursor, int? limit, CancellationToken ct = default)
    {
        if (limit is < 1) return Invalid<ChatThreadsResult>("Limit must be at least 1.");

        ChatThreadCursor? after = null;
        if (cursor is not null && !TryDecodeCursor(cursor, out after)) return Invalid<ChatThreadsResult>("Cursor is invalid.");

        var actor = await ResolveActorAsync(userId, role, ct);
        if (actor is null) return NotFound<ChatThreadsResult>();

        var size = Math.Min(limit ?? DefaultThreadsLimit, MaxThreadsLimit);
        var page = await chat.ListThreadsAsync(actor, after, size, ct);

        var next = page.HasMore && page.Items.Count > 0 ? EncodeCursor(page.Items[^1]) : null;

        return new ChatResult<ChatThreadsResult>(
            new ChatThreadsResult(page.Items.Select(t => ToView(t, actor)).ToList(), next), ChatError.None);
    }

    public async Task<ChatResult<ChatThreadView>> GetThreadAsync(
        Guid userId, Role role, Guid threadId, CancellationToken ct = default)
    {
        var actor = await ResolveActorAsync(userId, role, ct);
        var thread = actor is null ? null : await chat.FindThreadAsync(actor, threadId, ct);

        return thread is null || actor is null
            ? NotFound<ChatThreadView>()
            : new ChatResult<ChatThreadView>(ToView(thread, actor), ChatError.None);
    }

    public async Task<ChatResult<ChatMessagesResult>> GetMessagesAsync(
        Guid userId, Role role, Guid threadId, Guid? before, Guid? after, int? limit, CancellationToken ct = default)
    {
        if (before is not null && after is not null) return Invalid<ChatMessagesResult>("Use either 'before' or 'after', not both.");
        if (limit is < 1) return Invalid<ChatMessagesResult>("Limit must be at least 1.");

        var actor = await ResolveActorAsync(userId, role, ct);
        var thread = actor is null ? null : await chat.FindThreadAsync(actor, threadId, ct);
        if (thread is null || actor is null) return NotFound<ChatMessagesResult>();

        var page = await chat.GetMessagesAsync(threadId, before, after, Math.Min(limit ?? DefaultMessagesLimit, MaxMessagesLimit), ct);
        if (page is null) return Invalid<ChatMessagesResult>("Cursor message does not belong to the thread.");

        return new ChatResult<ChatMessagesResult>(
            new ChatMessagesResult(page.Items.Select(m => ToView(m, thread.BuyerId, actor.Side)).ToList(), page.HasMore),
            ChatError.None);
    }

    public async Task<ChatResult<ChatMessageView>> SendAsync(
        Guid userId, Role role, Guid threadId, string? body, CancellationToken ct = default)
    {
        var actor = await ResolveActorAsync(userId, role, ct);
        var thread = actor is null ? null : await chat.FindThreadAsync(actor, threadId, ct);
        if (thread is null || actor is null) return NotFound<ChatMessageView>();

        if (thread.IsLocked)
            return new ChatResult<ChatMessageView>(null, ChatError.ThreadLocked, "The conversation is locked.");

        var text = body?.Trim();
        if (string.IsNullOrEmpty(text)) return Invalid<ChatMessageView>("Message must not be empty.");
        if (text.Length > MaxBodyLength) return Invalid<ChatMessageView>($"Message must not exceed {MaxBodyLength} characters.");

        var message = new ChatMessage(Guid.NewGuid(), threadId, userId, text, null, clock.GetUtcNow());
        await chat.AddMessageAsync(message, ct);

        return new ChatResult<ChatMessageView>(ToView(message, thread.BuyerId, actor.Side), ChatError.None);
    }

    public async Task<ChatResult<bool>> MarkReadAsync(Guid userId, Role role, Guid threadId, CancellationToken ct = default)
    {
        var actor = await ResolveActorAsync(userId, role, ct);
        var thread = actor is null ? null : await chat.FindThreadAsync(actor, threadId, ct);
        if (thread is null || actor is null) return NotFound<bool>();

        await chat.MarkReadAsync(actor, threadId, clock.GetUtcNow(), ct);

        return new ChatResult<bool>(true, ChatError.None);
    }

    public async Task<ChatResult<int>> GetUnreadCountAsync(Guid userId, Role role, CancellationToken ct = default)
    {
        var actor = await ResolveActorAsync(userId, role, ct);
        if (actor is null) return NotFound<int>();

        return new ChatResult<int>(await chat.GetUnreadCountAsync(actor, ct), ChatError.None);
    }

    private async Task<ChatActor?> ResolveActorAsync(Guid userId, Role role, CancellationToken ct)
    {
        switch (role)
        {
            case Role.Buyer:
                return new ChatActor(userId, ChatSide.Buyer, null);
            case Role.Merchant:
                var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
                return merchantId is null ? null : new ChatActor(userId, ChatSide.Merchant, merchantId);
            default:
                return null;
        }
    }

    private static ChatThreadView ToView(ChatThreadInfo t, ChatActor actor)
    {
        var isBuyer = actor.Side == ChatSide.Buyer;

        return new ChatThreadView(
            t,
            actor.Side,
            isBuyer ? t.MerchantId : t.BuyerId,
            isBuyer ? t.MerchantName : BuyerNamePlaceholder,
            t.LastSenderId is { } sender && IsMine(sender, t.BuyerId, actor.Side));
    }

    private static ChatMessageView ToView(ChatMessage m, Guid buyerId, ChatSide side) =>
        new(m, IsMine(m.SenderId, buyerId, side));

    // A merchant's colleagues share the shop side of the conversation.
    private static bool IsMine(Guid senderId, Guid buyerId, ChatSide side) =>
        side == ChatSide.Buyer ? senderId == buyerId : senderId != buyerId;

    private sealed record CursorPayload(DateTimeOffset A, Guid I);

    private static string EncodeCursor(ChatThreadInfo last) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new CursorPayload(last.LastActivityAt, last.Id)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeCursor(string cursor, out ChatThreadCursor? result)
    {
        result = null;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');

            var payload = JsonSerializer.Deserialize<CursorPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            if (payload is null) return false;

            result = new ChatThreadCursor(payload.A, payload.I);
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return false;
        }
    }

    private static ChatResult<T> Invalid<T>(string message) => new(default, ChatError.Validation, message);

    private static ChatResult<T> NotFound<T>() => new(default, ChatError.NotFound, "Conversation not found.");
}