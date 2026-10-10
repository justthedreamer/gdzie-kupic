using System.Collections.Concurrent;
using Gdzie.Kupic.Chat;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Realtime;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>Records every chat and notification event and forwards it to the real channel.</summary>
public sealed class RecordingChatChannel(IRealtimeSender sender) : IChatChannel
{
    private readonly ChatChannel _inner = new(sender);
    private readonly ConcurrentQueue<(string Event, Guid UserId, Guid ThreadId, Guid? MessageId)> _events = new();

    public IReadOnlyList<(string Event, Guid UserId, Guid ThreadId, Guid? MessageId)> Events => _events.ToArray();

    public Task MessageReceivedAsync(Guid userId, Guid threadId, Guid messageId, CancellationToken ct = default)
    {
        _events.Enqueue(("messageReceived", userId, threadId, messageId));
        return _inner.MessageReceivedAsync(userId, threadId, messageId, ct);
    }

    public Task ThreadUpdatedAsync(Guid userId, Guid threadId, CancellationToken ct = default)
    {
        _events.Enqueue(("threadUpdated", userId, threadId, null));
        return _inner.ThreadUpdatedAsync(userId, threadId, ct);
    }

    public void Reset() => _events.Clear();
}

public sealed class RecordingNotificationChannel(IRealtimeSender sender) : INotificationChannel
{
    private readonly NotificationChannel _inner = new(sender);
    private readonly ConcurrentQueue<(Guid UserId, NotificationKind Kind, Guid? PostId, Guid? ThreadId)> _events = new();

    public IReadOnlyList<(Guid UserId, NotificationKind Kind, Guid? PostId, Guid? ThreadId)> Events => _events.ToArray();

    public Task NotificationRaisedAsync(Guid userId, NotificationKind kind, Guid? postId, Guid? threadId, CancellationToken ct = default)
    {
        _events.Enqueue((userId, kind, postId, threadId));
        return _inner.NotificationRaisedAsync(userId, kind, postId, threadId, ct);
    }

    public void Reset() => _events.Clear();
}
