namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum ResponseSaveStatus
{
    Saved,
    NotFound,
    PostNotActive,
}

public sealed record ResponseSaveResult(ResponseSaveStatus Status, MerchantResponse? Response = null, Guid? ThreadId = null);

/// <summary>A positive merchant response as the buyer sees it, with the buyer's unread chat messages.</summary>
public sealed record PostResponseInfo(
    Guid MerchantId,
    string ShopName,
    ResponseState State,
    Guid? ThreadId,
    int UnreadCount,
    DateTimeOffset UpdatedAt);

public interface IResponseStorage
{
    /// <summary>Number of responses of the post per state; states without responses are missing.</summary>
    Task<IReadOnlyDictionary<ResponseState, int>> CountByStateAsync(Guid postId, CancellationToken ct = default);

    /// <summary>Positive responses of the post, newest update first. Unread counts are the buyer's.</summary>
    Task<IReadOnlyList<PostResponseInfo>> ListPositiveAsync(Guid postId, Guid buyerId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the merchant's response and, for a positive state, creates the chat thread when it does not exist yet.
    /// The notification and open-post checks and the writes happen in one transaction; the post row is locked so a
    /// concurrent close cannot slip in between.
    /// </summary>
    Task<ResponseSaveResult> SaveAsync(
        Guid postId, Guid merchantId, ResponseState state, DateTimeOffset now, CancellationToken ct = default);
}