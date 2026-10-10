namespace Gdzie.Kupic.Chat;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Storage;

public enum ChatError
{
    None,
    Validation,
    NotFound,
    ThreadLocked,
}

public sealed record ChatResult<T>(T? Value, ChatError Error, string? Message = null)
{
    public bool IsSuccess => Error == ChatError.None;
}

/// <summary>A thread as seen by the caller; <see cref="CounterpartName"/> is null when the name is not known.</summary>
public sealed record ChatThreadView(ChatThreadInfo Thread, ChatSide Side, Guid CounterpartId, string? CounterpartName, bool LastMessageIsMine);

public sealed record ChatThreadsResult(IReadOnlyList<ChatThreadView> Items, string? NextCursor);

public sealed record ChatMessageView(ChatMessage Message, bool IsMine);

public sealed record ChatMessagesResult(IReadOnlyList<ChatMessageView> Items, bool HasMore);

public interface IChatService
{
    Task<ChatResult<ChatThreadsResult>> ListThreadsAsync(
        Guid userId, Role role, string? cursor, int? limit, CancellationToken ct = default);

    Task<ChatResult<ChatThreadView>> GetThreadAsync(Guid userId, Role role, Guid threadId, CancellationToken ct = default);

    Task<ChatResult<ChatMessagesResult>> GetMessagesAsync(
        Guid userId, Role role, Guid threadId, Guid? before, Guid? after, int? limit, CancellationToken ct = default);

    Task<ChatResult<ChatMessageView>> SendAsync(
        Guid userId, Role role, Guid threadId, string? body, CancellationToken ct = default);

    Task<ChatResult<bool>> MarkReadAsync(Guid userId, Role role, Guid threadId, CancellationToken ct = default);

    Task<ChatResult<int>> GetUnreadCountAsync(Guid userId, Role role, CancellationToken ct = default);
}