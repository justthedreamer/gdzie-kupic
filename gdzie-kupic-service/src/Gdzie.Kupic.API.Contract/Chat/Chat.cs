namespace Gdzie.Kupic.Service.API.Contract.Chat;

public sealed class Chat
{
    public sealed record ThreadPost(Guid Id, string Title, string Status);

    public sealed record Counterpart(Guid Id, string DisplayName);

    public sealed record LastMessage(string Preview, DateTimeOffset CreatedAt, bool IsMine);

    public sealed record ThreadSummary(
        Guid Id,
        ThreadPost Post,
        Counterpart Counterpart,
        LastMessage? LastMessage,
        int UnreadCount,
        bool IsLocked,
        DateTimeOffset CreatedAt);

    public sealed record ThreadPage(IReadOnlyList<ThreadSummary> Items, string? NextCursor);

    public sealed record Message(
        Guid Id,
        Guid ThreadId,
        Guid SenderId,
        bool IsMine,
        string? Body,
        string? AttachmentUrl,
        DateTimeOffset CreatedAt);

    public sealed record MessagePage(IReadOnlyList<Message> Items, bool HasMore);

    public sealed record UnreadCount(int Count);
}