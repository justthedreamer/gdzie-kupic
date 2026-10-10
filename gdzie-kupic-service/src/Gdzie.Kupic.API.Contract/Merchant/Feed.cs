using NamedRef = Gdzie.Kupic.Service.API.Contract.Posts.Posts.NamedRef;

namespace Gdzie.Kupic.Service.API.Contract.Merchant;

public sealed class Feed
{
    public sealed record RespondRequest(string State);

    public sealed record RespondResponse(string State, Guid? ThreadId, DateTimeOffset UpdatedAt);

    public record FeedItem(
        Guid Id,
        string Title,
        string? Description,
        NamedRef Category,
        NamedRef Tag,
        double DistanceKm,
        decimal? BuyerRadiusKm,
        string BuyerName,
        bool IsUrgent,
        DateTimeOffset? UrgentDeadline,
        DateTimeOffset ExpiresAt,
        string Status,
        DateTimeOffset CreatedAt,
        string? MyResponse);

    public sealed record FeedDetail(
        Guid Id,
        string Title,
        string? Description,
        NamedRef Category,
        NamedRef Tag,
        double DistanceKm,
        decimal? BuyerRadiusKm,
        string BuyerName,
        bool IsUrgent,
        DateTimeOffset? UrgentDeadline,
        DateTimeOffset ExpiresAt,
        string Status,
        DateTimeOffset CreatedAt,
        string? MyResponse,
        Guid? ThreadId)
        : FeedItem(Id, Title, Description, Category, Tag, DistanceKm, BuyerRadiusKm, BuyerName, IsUrgent,
            UrgentDeadline, ExpiresAt, Status, CreatedAt, MyResponse);

    public sealed record FeedPage(IReadOnlyList<FeedItem> Items, string? NextCursor);

    public sealed record Summary(int NewCount, int RespondedCount);
}