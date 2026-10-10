namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;

internal sealed class MerchantResponseService(
    IMarketplaceStorage marketplace,
    IResponseStorage responses,
    IPostStorage posts,
    PostFeedEvents events,
    IChatThreadEvents chatEvents,
    INotificationChannel notifications,
    INotificationDispatcher dispatcher,
    ILogger<MerchantResponseService> logger,
    TimeProvider clock) : IMerchantResponseService
{
    public async Task<ResponseOutcome> RespondAsync(Guid userId, Guid postId, ResponseState state, CancellationToken ct = default)
    {
        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return new ResponseOutcome(ResponseError.NotFound);

        var result = await responses.SaveAsync(postId, merchantId.Value, state, clock.GetUtcNow(), ct);

        switch (result.Status)
        {
            case ResponseSaveStatus.NotFound:
                return new ResponseOutcome(ResponseError.NotFound);
            case ResponseSaveStatus.PostNotActive:
                return new ResponseOutcome(ResponseError.PostNotActive);
        }

        await events.PostStatusChangedAsync(postId);
        if (result.ThreadCreated && result.ThreadId is { } threadId) await chatEvents.ThreadCreatedAsync(threadId);
        if (state.IsPositive() && result.PreviousState != state) await NotifyBuyerAsync(postId, result.ThreadId);

        return new ResponseOutcome(ResponseError.None, result.Response!.State, result.ThreadId, result.Response.UpdatedAt);
    }

    private async Task NotifyBuyerAsync(Guid postId, Guid? threadId)
    {
        try
        {
            var buyerId = await posts.FindOwnerIdAsync(postId);
            if (buyerId is not { } id) return;

            await notifications.NotificationRaisedAsync(id, NotificationKind.MerchantResponded, postId, threadId);
            await dispatcher.DispatchAsync(new Notification(NotificationKind.MerchantResponded, id, postId, threadId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push merchantResponded notification for post {PostId}", postId);
        }
    }
}
