namespace Gdzie.Kupic.Storage;

public sealed record PushSubscriptionInfo(Guid Id, Guid UserId, string Endpoint, string P256dhKey, string AuthKey);

public interface INotificationStorage
{
    Task<IReadOnlyList<Guid>> FindPushSubscriptionIdsAsync(Guid userId, CancellationToken ct = default);

    Task<PushSubscriptionInfo?> FindPushSubscriptionAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>Removes the subscription with the endpoint, whoever owns it; returns how many rows were removed.</summary>
    Task<int> RemovePushSubscriptionByEndpointAsync(string endpoint, CancellationToken ct = default);

    /// <summary>
    /// Fills in <c>Channel</c> / <c>SentAt</c> of the post notification of the user's merchant after a real delivery;
    /// an already delivered notification keeps its first values.
    /// </summary>
    Task MarkPostNotificationSentAsync(
        Guid postId, Guid userId, Domain.Model.Notifications.NotificationChannel channel, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Stores the subscription of the user, or updates the keys of the existing one with the same endpoint and
    /// moves it to the user when it belonged to somebody else.
    /// </summary>
    Task UpsertPushSubscriptionAsync(
        Guid userId, string endpoint, string p256dhKey, string authKey, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Removes the user's own subscription for the endpoint; false when there was none.</summary>
    Task<bool> RemovePushSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct = default);
}
