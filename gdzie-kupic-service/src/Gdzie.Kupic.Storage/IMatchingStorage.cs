namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Location;

public sealed record PostMatchCriteria(
    Guid PostId, Guid BuyerId, Coordinates Coordinates, decimal? RadiusKm, Guid CategoryId, Guid TagId);

public interface IMatchingStorage
{
    /// <summary>
    /// Ids of the merchants a post reaches: a branch within radius + tolerance (any distance for an
    /// unlimited radius) and a category-level or exact-tag subscription. Banned merchants and the
    /// post's author are excluded.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindMatchingMerchantIdsAsync(PostMatchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// Ids of the open posts (Active, not expired, regardless of dispatch status) that reach the merchant
    /// under the same rule and that the merchant has not been notified about yet. Empty for banned or
    /// unknown merchants; the merchant's own posts are excluded.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindMatchingOpenPostIdsAsync(Guid merchantId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Records one notification for the merchant per post; returns the posts that were newly notified.</summary>
    Task<IReadOnlyList<Guid>> AddMerchantNotificationsAsync(
        Guid merchantId, IReadOnlyCollection<Guid> postIds, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Records one notification per merchant; returns the merchants that were newly notified.</summary>
    Task<IReadOnlyList<Guid>> AddNotificationsAsync(
        Guid postId, IReadOnlyCollection<Guid> merchantIds, DateTimeOffset now, CancellationToken ct = default);
}
