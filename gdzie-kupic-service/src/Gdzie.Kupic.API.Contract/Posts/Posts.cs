namespace Gdzie.Kupic.Service.API.Contract.Posts;

public sealed class Posts
{
    public sealed record CreateRequest(
        double Latitude,
        double Longitude,
        decimal? RadiusKm,
        Guid CategoryId,
        Guid TagId,
        string? Title,
        string? Description,
        DateTimeOffset? UrgentDeadline);

    public sealed record StatusDto(
        string NotificationDispatchStatus,
        int NotifiedCount,
        int CheckingCount,
        int HaveItCount,
        int MayHaveItCount,
        int CanOrderItCount,
        int CannotHelpCount,
        bool IsZeroMatch);

    public sealed record ResponseItem(
        Guid MerchantId,
        string ShopName,
        string State,
        Guid? ThreadId,
        int UnreadCount,
        DateTimeOffset UpdatedAt);

    public sealed record NamedRef(Guid Id, string Name);

    public record PostDto(
        Guid Id,
        string Title,
        string? Description,
        double Latitude,
        double Longitude,
        decimal? RadiusKm,
        NamedRef Category,
        NamedRef Tag,
        string Status,
        string NotificationDispatchStatus,
        bool IsUrgent,
        DateTimeOffset? UrgentDeadline,
        DateTimeOffset ExpiresAt,
        bool IsLongLived,
        DateTimeOffset CreatedAt);

    public sealed record PostWithCountDto(
        Guid Id,
        string Title,
        string? Description,
        double Latitude,
        double Longitude,
        decimal? RadiusKm,
        NamedRef Category,
        NamedRef Tag,
        string Status,
        string NotificationDispatchStatus,
        bool IsUrgent,
        DateTimeOffset? UrgentDeadline,
        DateTimeOffset ExpiresAt,
        bool IsLongLived,
        DateTimeOffset CreatedAt,
        int NotifiedCount)
        : PostDto(Id, Title, Description, Latitude, Longitude, RadiusKm, Category, Tag, Status,
            NotificationDispatchStatus, IsUrgent, UrgentDeadline, ExpiresAt, IsLongLived, CreatedAt);
}
