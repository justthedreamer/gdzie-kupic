namespace Gdzie.Kupic.Notifications;

/// <summary>One notification for one recipient user account.</summary>
public sealed record Notification(NotificationKind Kind, Guid RecipientUserId, Guid? PostId = null, Guid? ThreadId = null);

/// <summary>
/// Hands a notification to the out-of-app delivery channels (Web Push). Users with an open real-time connection are
/// skipped: they already receive the in-app event. Never throws - a failure is logged.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>Number of open real-time connections per user, held in memory (single instance).</summary>
public interface IPresenceTracker
{
    bool IsOnline(Guid userId);
}

internal sealed class NullPresenceTracker : IPresenceTracker
{
    public bool IsOnline(Guid userId) => false;
}
