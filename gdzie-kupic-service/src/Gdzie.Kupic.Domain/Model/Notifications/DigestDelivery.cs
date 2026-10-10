namespace Gdzie.Kupic.Domain.Model.Notifications;

/// <summary>Records that the merchant digest of a schedule slot was sent to a user, so a re-run sends no duplicate.</summary>
public sealed class DigestDelivery(Guid id, Guid userId, DateTimeOffset slot, DateTimeOffset sentAt)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public DateTimeOffset Slot { get; init; } = slot;
    public DateTimeOffset SentAt { get; init; } = sentAt;
}
