namespace Gdzie.Kupic.Domain.Model.Infrastructure;

public static class OutboxMessageTypes
{
    public const string NotifyMerchants = "NotifyMerchants";
    public const string NotifyNewMerchant = "NotifyNewMerchant";
}

public sealed class OutboxMessage(
    Guid id,
    string type,
    string payload,
    DateTimeOffset createdAt,
    DateTimeOffset? processedAt = null)
{
    public Guid Id { get; init; } = id;
    public string Type { get; init; } = type;
    public string Payload { get; init; } = payload;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset? ProcessedAt { get; set; } = processedAt;
}
