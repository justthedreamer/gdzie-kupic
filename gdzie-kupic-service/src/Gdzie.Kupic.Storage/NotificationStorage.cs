namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Notifications;
using Microsoft.EntityFrameworkCore;

internal sealed class NotificationStorage(AppDbContext db) : INotificationStorage
{
    private const int MaxAttempts = 3;

    public async Task UpsertPushSubscriptionAsync(
        Guid userId, string endpoint, string p256dhKey, string authKey, DateTimeOffset now, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var subscription = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == endpoint, ct);
                var keys = new WebPushKeys(p256dhKey, authKey);

                if (subscription is null)
                {
                    db.PushSubscriptions.Add(new PushSubscription(Guid.NewGuid(), userId, endpoint, now) { Keys = keys });
                }
                else
                {
                    subscription.UserId = userId;
                    subscription.Keys = keys;
                }

                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Lost a race on the unique endpoint index; the retry sees the winner's row.
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<bool> RemovePushSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct = default)
    {
        var subscription = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
        if (subscription is null) return false;

        db.PushSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(ct);

        return true;
    }
}
