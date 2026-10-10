namespace Gdzie.Kupic.Notifications;

public enum NotificationKind
{
    MerchantResponded,
    NewMessage,
}

/// <summary>
/// In-app (real-time) notification push, implemented by the Realtime module. Recipient is a user id.
/// Web Push and e-mail are separate and not part of this channel.
/// </summary>
public interface INotificationChannel
{
    Task NotificationRaisedAsync(Guid userId, NotificationKind kind, Guid? postId, Guid? threadId, CancellationToken ct = default);
}

internal sealed class NullNotificationChannel : INotificationChannel
{
    public Task NotificationRaisedAsync(Guid userId, NotificationKind kind, Guid? postId, Guid? threadId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
