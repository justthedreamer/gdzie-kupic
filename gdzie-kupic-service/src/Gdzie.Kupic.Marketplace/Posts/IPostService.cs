namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum PostError
{
    None,
    Validation,
    NotFound,
    Conflict,
}

public enum PostScope
{
    Active,
    Ended,
}

public sealed record PostResult<T>(T? Value, PostError Error, string? Message = null)
{
    public bool IsSuccess => Error == PostError.None;
}

public sealed record CreatePostInput(
    double Latitude,
    double Longitude,
    decimal? RadiusKm,
    Guid CategoryId,
    Guid TagId,
    string? Title,
    string? Description,
    DateTimeOffset? UrgentDeadline);

/// <summary>A post (with category and tag loaded) and the number of merchants notified about it.</summary>
public sealed record PostView(Post Post, int NotifiedCount);

public interface IPostService
{
    Task<PostResult<PostView>> CreateAsync(Guid buyerId, CreatePostInput input, CancellationToken ct = default);

    Task<PostResult<IReadOnlyList<PostView>>> ListAsync(Guid buyerId, PostScope scope, CancellationToken ct = default);

    Task<PostResult<PostView>> GetAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    Task<PostResult<bool>> FulfilAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    Task<PostResult<bool>> CloseAsync(Guid buyerId, Guid postId, CancellationToken ct = default);
}
