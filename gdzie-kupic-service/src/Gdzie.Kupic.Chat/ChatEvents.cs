namespace Gdzie.Kupic.Chat;

using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Thread events that other modules raise once their change is committed.</summary>
public interface IChatThreadEvents
{
    /// <summary>The thread was created (first positive merchant response); both participants get <c>threadUpdated</c>.</summary>
    Task ThreadCreatedAsync(Guid threadId);

    /// <summary>
    /// The lock flag of the threads changed (see <see cref="IChatStorage.SetLockForUserAsync"/>); both participants of each
    /// thread get <c>threadUpdated</c>. Call after the transaction that changed the flags is committed.
    /// </summary>
    Task ThreadsLockChangedAsync(IReadOnlyCollection<Guid> threadIds);
}

/// <summary>Resolves recipients and pushes chat events. A push failure is logged and never propagates.</summary>
internal sealed class ChatEvents(
    IChatStorage chat,
    IChatChannel channel,
    INotificationChannel notifications,
    INotificationDispatcher dispatcher,
    ILogger<ChatEvents> logger) : IChatThreadEvents
{
    public Task ThreadCreatedAsync(Guid threadId) => ThreadUpdatedAsync([threadId]);

    public Task ThreadsLockChangedAsync(IReadOnlyCollection<Guid> threadIds) => ThreadUpdatedAsync(threadIds);

    /// <summary>The other side of the sender gets <c>messageReceived</c> and an in-app <c>newMessage</c> notification.</summary>
    public async Task MessageReceivedAsync(Guid threadId, Guid messageId, ChatSide senderSide)
    {
        try
        {
            var participants = await chat.FindParticipantsAsync(threadId);
            if (participants is null) return;

            IReadOnlyList<Guid> recipients = senderSide == ChatSide.Buyer ? participants.MerchantUserIds : [participants.BuyerId];
            var startsSeries = senderSide == ChatSide.Merchant && await chat.StartsUnreadSeriesForBuyerAsync(threadId, messageId);
            foreach (var userId in recipients)
            {
                await channel.MessageReceivedAsync(userId, threadId, messageId);
                await notifications.NotificationRaisedAsync(userId, NotificationKind.NewMessage, null, threadId);
                await dispatcher.DispatchAsync(new Notification(NotificationKind.NewMessage, userId, null, threadId, startsSeries));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push messageReceived for thread {ThreadId}", threadId);
        }
    }

    private async Task ThreadUpdatedAsync(IReadOnlyCollection<Guid> threadIds)
    {
        foreach (var threadId in threadIds)
        {
            try
            {
                var participants = await chat.FindParticipantsAsync(threadId);
                if (participants is null) continue;

                foreach (var userId in participants.MerchantUserIds.Append(participants.BuyerId))
                    await channel.ThreadUpdatedAsync(userId, threadId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to push threadUpdated for thread {ThreadId}", threadId);
            }
        }
    }
}
