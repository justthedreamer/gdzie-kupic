namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum FeedTab
{
    New,
    Responded,
    All,
}

public enum FeedSort
{
    Newest,
    Nearest,
}

/// <summary>Position of the last item of the previous page; which fields are used depends on the sort.</summary>
public sealed record FeedCursor(bool IsUrgent, DateTimeOffset CreatedAt, double DistanceKm, Guid PostId);

public sealed record FeedQuery(
    Guid MerchantId,
    FeedTab Tab,
    Guid? CategoryId,
    double? MaxDistanceKm,
    FeedSort Sort,
    FeedCursor? After,
    int Limit,
    DateTimeOffset Now);

/// <summary>A post (with category and tag loaded) as seen by one merchant.</summary>
public sealed record FeedEntry(Post Post, double DistanceKm, ResponseState? MyResponse, Guid? ThreadId);

public sealed record FeedPage(IReadOnlyList<FeedEntry> Items, bool HasMore);

public sealed record FeedSummary(int NewCount, int RespondedCount);

public interface IFeedStorage
{
    /// <summary>Active, non-expired posts the merchant was notified about, filtered, ordered and keyset-paginated.</summary>
    Task<FeedPage> GetFeedAsync(FeedQuery query, CancellationToken ct = default);

    Task<FeedSummary> GetSummaryAsync(Guid merchantId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>The post in any status, or null when the merchant was not notified about it.</summary>
    Task<FeedEntry?> FindEntryAsync(Guid merchantId, Guid postId, CancellationToken ct = default);
}