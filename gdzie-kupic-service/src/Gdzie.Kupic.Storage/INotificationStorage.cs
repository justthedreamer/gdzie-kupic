namespace Gdzie.Kupic.Storage;

public interface INotificationStorage
{
    /// <summary>
    /// Stores the subscription of the user, or updates the keys of the existing one with the same endpoint and
    /// moves it to the user when it belonged to somebody else.
    /// </summary>
    Task UpsertPushSubscriptionAsync(
        Guid userId, string endpoint, string p256dhKey, string authKey, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Removes the user's own subscription for the endpoint; false when there was none.</summary>
    Task<bool> RemovePushSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct = default);
}
