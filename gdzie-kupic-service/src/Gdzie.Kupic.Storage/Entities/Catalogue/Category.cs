namespace Gdzie.Kupic.Storage.Entities.Catalogue;

public sealed class Category(
    Guid id,
    string name,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public string Name { get; init; } = name;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public ICollection<Tag> Tags { get; init; } = [];
}

