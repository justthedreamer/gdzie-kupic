namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Storage;

public enum FeedError
{
    None,
    Validation,
    NotFound,
}

public sealed record FeedResult<T>(T? Value, FeedError Error, string? Message = null)
{
    public bool IsSuccess => Error == FeedError.None;
}

public sealed record FeedRequest(
    FeedTab Tab,
    Guid? CategoryId,
    double? MaxDistanceKm,
    FeedSort Sort,
    string? Cursor,
    int? Limit);

public sealed record FeedItemsPage(IReadOnlyList<FeedEntry> Items, string? NextCursor);

public interface IMerchantFeedService
{
    Task<FeedResult<FeedItemsPage>> GetFeedAsync(Guid userId, FeedRequest request, CancellationToken ct = default);

    Task<FeedResult<FeedSummary>> GetSummaryAsync(Guid userId, CancellationToken ct = default);

    Task<FeedResult<FeedEntry>> GetEntryAsync(Guid userId, Guid postId, CancellationToken ct = default);
}