namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Storage;

internal sealed class MerchantResponseService(
    IMarketplaceStorage marketplace,
    IResponseStorage responses,
    PostFeedEvents events,
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

        return new ResponseOutcome(ResponseError.None, result.Response!.State, result.ThreadId, result.Response.UpdatedAt);
    }
}