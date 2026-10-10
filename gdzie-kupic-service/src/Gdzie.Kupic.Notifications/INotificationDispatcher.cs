namespace Gdzie.Kupic.Notifications;

/// <summary>Hands a recorded post notification to the delivery channels (Web Push / email).</summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(Guid postId, Guid merchantId, CancellationToken ct = default);
}
