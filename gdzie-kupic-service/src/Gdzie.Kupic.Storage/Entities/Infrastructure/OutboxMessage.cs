namespace Gdzie.Kupic.Storage.Entities.Infrastructure;

public sealed class OutboxMessage(
    Guid id,
    string type,
    string payload,
    DateTimeOffset createdAt,
    DateTimeOffset? processedAt)
{
    public Guid Id { get; init; } = id;
    public string Type { get; init; } = type;
    public string Payload { get; init; } = payload;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset? ProcessedAt { get; init; } = processedAt;
}

