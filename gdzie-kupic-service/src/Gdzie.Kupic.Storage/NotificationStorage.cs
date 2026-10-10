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

    public async Task<bool?> GetEmailEnabledAsync(Guid userId, CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (bool?)u.EmailNotificationsEnabled).SingleOrDefaultAsync(ct);

    public async Task<bool> SetEmailEnabledAsync(Guid userId, bool enabled, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;

        user.EmailNotificationsEnabled = enabled;
        await db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<EmailRecipient?> FindEmailRecipientAsync(Guid userId, CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new EmailRecipient(u.Id, u.Email, u.FirstName, u.Role, u.EmailNotificationsEnabled, u.BanDetails != null))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindPushSubscriptionIdsAsync(Guid userId, CancellationToken ct = default) =>
        await db.PushSubscriptions.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Id).ToListAsync(ct);

    public async Task<PushSubscriptionInfo?> FindPushSubscriptionAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var s = await db.PushSubscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subscriptionId, ct);

        return s is null ? null : new PushSubscriptionInfo(s.Id, s.UserId, s.Endpoint, s.Keys.P256dhKey, s.Keys.AuthKey);
    }

    public async Task<int> RemovePushSubscriptionByEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        var rows = await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ToListAsync(ct);
        db.PushSubscriptions.RemoveRange(rows);
        await db.SaveChangesAsync(ct);

        return rows.Count;
    }

    public async Task MarkPostNotificationSentAsync(
        Guid postId, Guid userId, NotificationChannel channel, DateTimeOffset now, CancellationToken ct = default)
    {
        var merchantIds = db.MerchantAccounts.Where(a => a.UserId == userId).Select(a => a.MerchantId);
        var rows = await db.PostNotifications
            .Where(n => n.PostId == postId && merchantIds.Contains(n.MerchantId) && n.SentAt == null)
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        foreach (var row in rows)
        {
            row.Channel = channel;
            row.SentAt = now;
        }

        await db.SaveChangesAsync(ct);
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
