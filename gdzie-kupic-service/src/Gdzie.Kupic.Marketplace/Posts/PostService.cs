namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Options;

internal sealed class PostService(
    IPostStorage posts,
    IResponseStorage responses,
    ICatalogueStorage catalogue,
    IOptions<MarketplaceSettings> settings,
    TimeProvider clock) : IPostService
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int ListLimit = 50;

    private static readonly TimeSpan MinUrgentLead = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxUrgentLead = TimeSpan.FromHours(72);

    public async Task<PostResult<PostView>> CreateAsync(Guid buyerId, CreatePostInput input, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var title = input.Title?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();

        var error = await ValidateAsync(input, title, description, now, ct);
        if (error is not null) return new PostResult<PostView>(null, PostError.Validation, error);

        var post = new Post(
            Guid.NewGuid(),
            buyerId,
            new Coordinates(input.Latitude, input.Longitude),
            input.RadiusKm,
            input.CategoryId,
            input.TagId,
            title,
            description,
            input.UrgentDeadline,
            Post.CalculateExpiry(now, input.UrgentDeadline, settings.Value.DefaultPostLifetime),
            now);

        await posts.AddWithOutboxAsync(post, OutboxMessages.NotifyMerchants(post.Id, now), ct);

        return await GetAsync(buyerId, post.Id, ct);
    }

    public async Task<PostResult<IReadOnlyList<PostView>>> ListAsync(Guid buyerId, PostScope scope, CancellationToken ct = default)
    {
        var items = await posts.ListBuyerPostsAsync(buyerId, scope == PostScope.Active, ListLimit, ct);

        var counts = await posts.GetNotifiedCountsAsync(items.Select(p => p.Id).ToList(), ct);

        return new PostResult<IReadOnlyList<PostView>>(
            items.Select(p => new PostView(p, counts.GetValueOrDefault(p.Id))).ToList(), PostError.None);
    }

    public async Task<PostResult<PostView>> GetAsync(Guid buyerId, Guid postId, CancellationToken ct = default)
    {
        var post = await posts.FindBuyerPostAsync(postId, buyerId, ct);

        return post is null
            ? new PostResult<PostView>(null, PostError.NotFound, PostNotFoundMessage)
            : new PostResult<PostView>(new PostView(post, await NotifiedCountAsync(post.Id, ct)), PostError.None);
    }

    public async Task<PostResult<PostStatusView>> GetStatusAsync(Guid buyerId, Guid postId, CancellationToken ct = default)
    {
        var post = await posts.FindBuyerPostAsync(postId, buyerId, ct);
        if (post is null) return new PostResult<PostStatusView>(null, PostError.NotFound, PostNotFoundMessage);

        var notified = await NotifiedCountAsync(post.Id, ct);
        var counts = await responses.CountByStateAsync(post.Id, ct);
        var responded = counts.Values.Sum();

        return new PostResult<PostStatusView>(
            new PostStatusView(
                post.NotificationDispatchStatus,
                notified,
                Math.Max(0, notified - responded),
                counts.GetValueOrDefault(ResponseState.HaveIt),
                counts.GetValueOrDefault(ResponseState.MayHaveIt),
                counts.GetValueOrDefault(ResponseState.CanOrderIt),
                counts.GetValueOrDefault(ResponseState.CantHelp)),
            PostError.None);
    }

    public async Task<PostResult<IReadOnlyList<PostResponseInfo>>> GetResponsesAsync(
        Guid buyerId, Guid postId, CancellationToken ct = default)
    {
        var post = await posts.FindBuyerPostAsync(postId, buyerId, ct);
        if (post is null) return new PostResult<IReadOnlyList<PostResponseInfo>>(null, PostError.NotFound, PostNotFoundMessage);

        return new PostResult<IReadOnlyList<PostResponseInfo>>(await responses.ListPositiveAsync(post.Id, buyerId, ct), PostError.None);
    }

    public async Task<PostResult<PostView>> MakeLongLivedAsync(Guid buyerId, Guid postId, CancellationToken ct = default)
    {
        var post = await posts.FindBuyerPostForUpdateAsync(postId, buyerId, ct);
        if (post is null) return new PostResult<PostView>(null, PostError.NotFound, PostNotFoundMessage);

        var notified = await NotifiedCountAsync(post.Id, ct);

        if (!post.TryMakeLongLived(clock.GetUtcNow(), notified, settings.Value.LongLivedPostLifetime))
        {
            return new PostResult<PostView>(null, PostError.Conflict,
                "Only an active, non-urgent post with finished dispatch and no matching merchants can become long-lived.");
        }

        await posts.SaveChangesAsync(ct);

        return await GetAsync(buyerId, postId, ct);
    }

    public Task<PostResult<bool>> FulfilAsync(Guid buyerId, Guid postId, CancellationToken ct = default) =>
        EndAsync(buyerId, postId, (post, now) => post.TryFulfil(now), ct);

    public Task<PostResult<bool>> CloseAsync(Guid buyerId, Guid postId, CancellationToken ct = default) =>
        EndAsync(buyerId, postId, (post, now) => post.TryClose(now), ct);

    private async Task<PostResult<bool>> EndAsync(
        Guid buyerId, Guid postId, Func<Post, DateTimeOffset, bool> transition, CancellationToken ct)
    {
        var post = await posts.FindBuyerPostForUpdateAsync(postId, buyerId, ct);
        if (post is null) return new PostResult<bool>(false, PostError.NotFound, PostNotFoundMessage);

        if (!transition(post, clock.GetUtcNow()))
            return new PostResult<bool>(false, PostError.Conflict, "Only an active, non-expired post can be ended.");

        await posts.SaveChangesAsync(ct);
        return new PostResult<bool>(true, PostError.None);
    }

    private async Task<int> NotifiedCountAsync(Guid postId, CancellationToken ct) =>
        (await posts.GetNotifiedCountsAsync([postId], ct)).GetValueOrDefault(postId);

    private async Task<string?> ValidateAsync(
        CreatePostInput input, string title, string? description, DateTimeOffset now, CancellationToken ct)
    {
        if (title.Length == 0) return "Title must not be blank.";
        if (title.Length > MaxTitleLength) return $"Title must not exceed {MaxTitleLength} characters.";
        if (description?.Length > MaxDescriptionLength) return $"Description must not exceed {MaxDescriptionLength} characters.";

        if (!double.IsFinite(input.Latitude) || input.Latitude is < -90 or > 90) return "Latitude must be between -90 and 90.";
        if (!double.IsFinite(input.Longitude) || input.Longitude is < -180 or > 180) return "Longitude must be between -180 and 180.";
        if (input.RadiusKm <= 0) return "Radius must be greater than 0, or null for unlimited.";

        if (input.UrgentDeadline is { } deadline &&
            (deadline < now + MinUrgentLead || deadline > now + MaxUrgentLead))
        {
            return "Urgent deadline must be between 1 and 72 hours from now.";
        }

        var category = await catalogue.FindCategoryAsync(input.CategoryId, ct);
        if (category is null) return "Category does not exist.";
        if (category.IsDisabled) return "Category is disabled.";

        var tag = await catalogue.FindTagAsync(input.TagId, ct);
        if (tag is null) return "Tag does not exist.";
        if (tag.CategoryId != input.CategoryId) return "Tag does not belong to the category.";
        if (tag.IsDisabled) return "Tag is disabled.";

        return null;
    }

    private const string PostNotFoundMessage = "Post not found.";
}
