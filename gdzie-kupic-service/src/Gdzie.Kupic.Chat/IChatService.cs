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
    AttachmentTooLarge,
    UnsupportedAttachmentType,
}

public sealed record ChatResult<T>(T? Value, ChatError Error, string? Message = null)
{
    public bool IsSuccess => Error == ChatError.None;
}

/// <summary>A thread as seen by the caller; <see cref="CounterpartName"/> is null when the name is not known.</summary>
public sealed record ChatThreadView(ChatThreadInfo Thread, ChatSide Side, Guid CounterpartId, string? CounterpartName, bool LastMessageIsMine);

public sealed record ChatThreadsResult(IReadOnlyList<ChatThreadView> Items, string? NextCursor);

/// <summary>An uploaded image; <see cref="ContentType"/> is what the client declared.</summary>
public sealed record ChatAttachmentInput(Stream Content, string? ContentType, long Length);

public sealed record ChatMessageView(ChatMessage Message, bool IsMine, string? AttachmentUrl = null);

public sealed record ChatMessagesResult(IReadOnlyList<ChatMessageView> Items, bool HasMore);

public interface IChatService
{
    Task<ChatResult<ChatThreadsResult>> ListThreadsAsync(
        Guid userId, Role role, string? cursor, int? limit, CancellationToken ct = default);

    Task<ChatResult<ChatThreadView>> GetThreadAsync(Guid userId, Role role, Guid threadId, CancellationToken ct = default);

    Task<ChatResult<ChatMessagesResult>> GetMessagesAsync(
        Guid userId, Role role, Guid threadId, Guid? before, Guid? after, int? limit, CancellationToken ct = default);

    Task<ChatResult<ChatMessageView>> SendAsync(
        Guid userId, Role role, Guid threadId, string? body, ChatAttachmentInput? image, CancellationToken ct = default);

    Task<ChatResult<bool>> MarkReadAsync(Guid userId, Role role, Guid threadId, CancellationToken ct = default);

    Task<ChatResult<int>> GetUnreadCountAsync(Guid userId, Role role, CancellationToken ct = default);
}