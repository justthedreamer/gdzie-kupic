namespace Gdzie.Kupic.Domain.Model.Catalogue;

public sealed class Tag(
    Guid id,
    Guid categoryId,
    string name,
    bool isDisabled,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid CategoryId { get; init; } = categoryId;
    public string Name { get; set; } = name;
    public bool IsDisabled { get; set; } = isDisabled;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;

    public Category Category { get; init; } = null!;
}

