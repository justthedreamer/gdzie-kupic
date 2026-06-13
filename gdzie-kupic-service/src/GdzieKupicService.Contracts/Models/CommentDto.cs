namespace GdzieKupicService.API.Contracts;

public sealed class CommentDto
{
    public Guid Id { get; init; }
    public string Content { get; init; }
    public DateTime CreatedAt { get; init; }
}