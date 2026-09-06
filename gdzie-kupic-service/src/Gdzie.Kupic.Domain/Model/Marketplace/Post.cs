namespace Gdzie.Kupic.Domain.Model.Marketplace;

using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;

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

// TODO: Re-link to a buyer/merchant model once it is reintroduced (Phase 3).
public sealed class Post(
    Guid id,
    Guid buyerId,
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
    public Guid BuyerId { get; init; } = buyerId;
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

    public Category Category { get; init; } = null!;
    public Tag Tag { get; init; } = null!;
}
