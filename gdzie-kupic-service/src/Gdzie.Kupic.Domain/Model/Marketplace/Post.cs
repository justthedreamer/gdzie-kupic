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

public sealed class Post(
    Guid id,
    Guid buyerId,
    Coordinates coordinates,
    decimal? radiusKm,
    Guid categoryId,
    Guid tagId,
    string title,
    string? description,
    DateTimeOffset? urgentDeadline,
    DateTimeOffset expiresAt,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid BuyerId { get; init; } = buyerId;
    public Coordinates Coordinates { get; init; } = coordinates;

    /// <summary>Null means unlimited radius.</summary>
    public decimal? RadiusKm { get; init; } = radiusKm;

    public Guid CategoryId { get; init; } = categoryId;
    public Guid TagId { get; init; } = tagId;
    public string Title { get; init; } = title;
    public string? Description { get; init; } = description;
    public DateTimeOffset? UrgentDeadline { get; init; } = urgentDeadline;
    public PostStatus Status { get; private set; } = PostStatus.Active;
    public NotificationDispatchStatus NotificationDispatchStatus { get; private set; } = NotificationDispatchStatus.Pending;
    public DateTimeOffset ExpiresAt { get; private set; } = expiresAt;
    public bool IsLongLived { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; private set; } = createdAt;

    public Category Category { get; init; } = null!;
    public Tag Tag { get; init; } = null!;

    public bool IsUrgent => UrgentDeadline is not null;

    /// <summary>A post is only open while it is Active and not past its expiry, even if the expiry job has not run yet.</summary>
    public bool IsOpen(DateTimeOffset now) => Status == PostStatus.Active && now < ExpiresAt;

    public static DateTimeOffset CalculateExpiry(DateTimeOffset now, DateTimeOffset? urgentDeadline, TimeSpan defaultLifetime) =>
        urgentDeadline ?? now + defaultLifetime;

    public bool TryFulfil(DateTimeOffset now) => TryEnd(PostStatus.Fulfilled, now);

    public bool TryClose(DateTimeOffset now) => TryEnd(PostStatus.Closed, now);

    /// <summary>Idempotent: only an Active post past its expiry transitions.</summary>
    public bool TryExpire(DateTimeOffset now)
    {
        if (Status != PostStatus.Active || now < ExpiresAt) return false;

        Status = PostStatus.Expired;
        UpdatedAt = now;
        return true;
    }

    public void MarkDispatched(DateTimeOffset now)
    {
        if (NotificationDispatchStatus == NotificationDispatchStatus.Dispatched) return;

        NotificationDispatchStatus = NotificationDispatchStatus.Dispatched;
        UpdatedAt = now;
    }

    /// <summary>
    /// Eligible only while Active and open, once dispatch finished, for non-urgent posts that are
    /// not yet long-lived and did not notify any merchant.
    /// </summary>
    public bool CanBecomeLongLived(DateTimeOffset now, int notifiedCount) =>
        IsOpen(now)
        && NotificationDispatchStatus == NotificationDispatchStatus.Dispatched
        && !IsUrgent
        && !IsLongLived
        && notifiedCount == 0;

    public bool TryMakeLongLived(DateTimeOffset now, int notifiedCount, TimeSpan lifetime)
    {
        if (!CanBecomeLongLived(now, notifiedCount)) return false;

        IsLongLived = true;
        ExpiresAt = now + lifetime;
        UpdatedAt = now;
        return true;
    }

    private bool TryEnd(PostStatus target, DateTimeOffset now)
    {
        if (!IsOpen(now)) return false;

        Status = target;
        UpdatedAt = now;
        return true;
    }
}
