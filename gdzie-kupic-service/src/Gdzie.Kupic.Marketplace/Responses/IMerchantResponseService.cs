namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum ResponseError
{
    None,
    NotFound,
    PostNotActive,
}

public sealed record ResponseOutcome(ResponseError Error, ResponseState? State = null, Guid? ThreadId = null, DateTimeOffset? UpdatedAt = null)
{
    public bool IsSuccess => Error == ResponseError.None;
}

public interface IMerchantResponseService
{
    Task<ResponseOutcome> RespondAsync(Guid userId, Guid postId, ResponseState state, CancellationToken ct = default);
}