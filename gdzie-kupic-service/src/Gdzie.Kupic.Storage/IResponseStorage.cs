namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum ResponseSaveStatus
{
    Saved,
    NotFound,
    PostNotActive,
}

public sealed record ResponseSaveResult(ResponseSaveStatus Status, MerchantResponse? Response = null, Guid? ThreadId = null);

public interface IResponseStorage
{
    /// <summary>
    /// Upserts the merchant's response and, for a positive state, creates the chat thread when it does not exist yet.
    /// The notification and open-post checks and the writes happen in one transaction; the post row is locked so a
    /// concurrent close cannot slip in between.
    /// </summary>
    Task<ResponseSaveResult> SaveAsync(
        Guid postId, Guid merchantId, ResponseState state, DateTimeOffset now, CancellationToken ct = default);
}