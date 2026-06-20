namespace Gdzie.Kupic.Storage.Entities.Marketplace;

using Gdzie.Kupic.Storage.Entities.Auth;
using Gdzie.Kupic.Storage.Entities.Catalogue;
using Gdzie.Kupic.Storage.Entities.Location;

public enum PostStatus
{
    Active,
    Fulfilled,
    Closed,
    Expired,
}

public enum NotificationDispatchStatus
{
    Pending,
    Dispatched,
}

public sealed class Post(
    Guid id,
    Guid buyerProfileId,
    Coordinates coordinates,
    decimal radiusKm,
    Guid categoryId,
    Guid tagId,
    string title,
    string? description,
    PostStatus status,
    NotificationDispatchStatus notificationDispatchStatus,
    DateTimeOffset expiresAt,
    bool isLongLived,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt)
{
    public Guid Id { get; init; } = id;
    public Guid BuyerProfileId { get; init; } = buyerProfileId;
    public Coordinates Coordinates { get; init; } = coordinates;
    public decimal RadiusKm { get; init; } = radiusKm;
    public Guid CategoryId { get; init; } = categoryId;
    public Guid TagId { get; init; } = tagId;
    public string Title { get; init; } = title;
    public string? Description { get; init; } = description;
    public UrgencyDetails? Urgency { get; init; }
    public PostStatus Status { get; init; } = status;
    public NotificationDispatchStatus NotificationDispatchStatus { get; init; } = notificationDispatchStatus;
    public DateTimeOffset ExpiresAt { get; init; } = expiresAt;
    public bool IsLongLived { get; init; } = isLongLived;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; init; } = updatedAt;

    public BuyerProfile BuyerProfile { get; init; } = null!;
    public Category Category { get; init; } = null!;
    public Tag Tag { get; init; } = null!;

    public ICollection<MerchantResponse> MerchantResponses { get; init; } = [];
}

