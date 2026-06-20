namespace Gdzie.Kupic.Storage.Entities.Chat;

using Gdzie.Kupic.Storage.Entities.Auth;

public sealed class ChatMessage(
    Guid id,
    Guid threadId,
    Guid senderId,
    string? body,
    string? attachmentKey,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid ThreadId { get; init; } = threadId;
    public Guid SenderId { get; init; } = senderId;
    public string? Body { get; init; } = body;
    public string? AttachmentKey { get; init; } = attachmentKey;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public ChatThread Thread { get; init; } = null!;
    public User Sender { get; init; } = null!;
}

