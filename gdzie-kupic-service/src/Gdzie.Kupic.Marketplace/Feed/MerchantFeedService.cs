namespace Gdzie.Kupic.Marketplace;

using System.Text;
using System.Text.Json;
using Gdzie.Kupic.Storage;

internal sealed class MerchantFeedService(
    IMarketplaceStorage marketplace,
    IFeedStorage feed,
    TimeProvider clock) : IMerchantFeedService
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<FeedResult<FeedItemsPage>> GetFeedAsync(Guid userId, FeedRequest request, CancellationToken ct = default)
    {
        if (request.Limit is < 1) return Invalid<FeedItemsPage>("Limit must be at least 1.");
        if (request.MaxDistanceKm is < 0 || request.MaxDistanceKm is { } km && !double.IsFinite(km))
            return Invalid<FeedItemsPage>("Max distance must not be negative.");

        FeedCursor? after = null;
        if (request.Cursor is not null && !TryDecodeCursor(request.Cursor, out after))
            return Invalid<FeedItemsPage>("Cursor is invalid.");

        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return NotFound<FeedItemsPage>();

        var limit = Math.Min(request.Limit ?? DefaultLimit, MaxLimit);

        var page = await feed.GetFeedAsync(
            new FeedQuery(merchantId.Value, request.Tab, request.CategoryId, request.MaxDistanceKm, request.Sort, after, limit, clock.GetUtcNow()),
            ct);

        var next = page.HasMore && page.Items.Count > 0 ? EncodeCursor(page.Items[^1]) : null;

        return new FeedResult<FeedItemsPage>(new FeedItemsPage(page.Items, next), FeedError.None);
    }

    public async Task<FeedResult<FeedSummary>> GetSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return NotFound<FeedSummary>();

        return new FeedResult<FeedSummary>(await feed.GetSummaryAsync(merchantId.Value, clock.GetUtcNow(), ct), FeedError.None);
    }

    public async Task<FeedResult<FeedEntry>> GetEntryAsync(Guid userId, Guid postId, CancellationToken ct = default)
    {
        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return NotFound<FeedEntry>();

        var entry = await feed.FindEntryAsync(merchantId.Value, postId, ct);

        return entry is null ? NotFound<FeedEntry>() : new FeedResult<FeedEntry>(entry, FeedError.None);
    }

    private sealed record CursorPayload(bool U, DateTimeOffset C, double D, Guid I);

    private static string EncodeCursor(FeedEntry last)
    {
        var payload = new CursorPayload(last.Post.IsUrgent, last.Post.CreatedAt, last.DistanceKm, last.Post.Id);

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryDecodeCursor(string cursor, out FeedCursor? result)
    {
        result = null;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');

            var payload = JsonSerializer.Deserialize<CursorPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            if (payload is null || !double.IsFinite(payload.D)) return false;

            result = new FeedCursor(payload.U, payload.C, payload.D, payload.I);
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return false;
        }
    }

    private static FeedResult<T> Invalid<T>(string message) => new(default, FeedError.Validation, message);

    private static FeedResult<T> NotFound<T>() => new(default, FeedError.NotFound, "Post not found.");
}