namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Storage;

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

/// <summary>Live dispatch/response summary of a post.</summary>
public sealed record PostStatusView(
    NotificationDispatchStatus NotificationDispatchStatus,
    int NotifiedCount,
    int CheckingCount,
    int HaveItCount,
    int MayHaveItCount,
    int CanOrderItCount,
    int CannotHelpCount)
{
    public bool IsZeroMatch => NotificationDispatchStatus == NotificationDispatchStatus.Dispatched && NotifiedCount == 0;
}

public interface IPostService
{
    Task<PostResult<PostView>> CreateAsync(Guid buyerId, CreatePostInput input, CancellationToken ct = default);

    Task<PostResult<IReadOnlyList<PostView>>> ListAsync(Guid buyerId, PostScope scope, CancellationToken ct = default);

    Task<PostResult<PostView>> GetAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    Task<PostResult<PostStatusView>> GetStatusAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    /// <summary>Merchants with a positive response (CantHelp is excluded), newest update first.</summary>
    Task<PostResult<IReadOnlyList<PostResponseInfo>>> GetResponsesAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    Task<PostResult<PostView>> MakeLongLivedAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    Task<PostResult<bool>> FulfilAsync(Guid buyerId, Guid postId, CancellationToken ct = default);

    Task<PostResult<bool>> CloseAsync(Guid buyerId, Guid postId, CancellationToken ct = default);
}
