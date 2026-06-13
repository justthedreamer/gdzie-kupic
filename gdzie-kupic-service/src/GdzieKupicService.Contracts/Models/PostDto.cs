namespace GdzieKupicService.API.Contracts;

public sealed class PostDto
{
    public Guid Id { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public IEnumerable<CommentDto> Comments { get; init; }
}