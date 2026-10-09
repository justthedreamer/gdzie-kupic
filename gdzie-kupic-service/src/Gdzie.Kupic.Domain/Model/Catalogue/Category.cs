namespace Gdzie.Kupic.Domain.Model.Catalogue;

public sealed class Category(
    Guid id,
    string name,
    bool isDisabled,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public string Name { get; set; } = name;
    public bool IsDisabled { get; set; } = isDisabled;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public ICollection<Tag> Tags { get; init; } = [];
}
