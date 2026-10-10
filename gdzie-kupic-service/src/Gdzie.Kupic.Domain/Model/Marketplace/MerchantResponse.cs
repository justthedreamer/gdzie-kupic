namespace Gdzie.Kupic.Domain.Model.Marketplace;

public enum ResponseState
{
    CantHelp,
    MayHaveIt,
    HaveIt,
    CanOrderIt,
}

public static class ResponseStates
{
    public static bool IsPositive(this ResponseState state) => state != ResponseState.CantHelp;
}

public sealed class MerchantResponse(
    Guid id,
    Guid postId,
    Guid merchantId,
    ResponseState state,
    DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public Guid PostId { get; init; } = postId;
    public Guid MerchantId { get; init; } = merchantId;
    public ResponseState State { get; private set; } = state;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset UpdatedAt { get; private set; } = createdAt;

    public Post Post { get; init; } = null!;

    /// <summary>Any state may be replaced by any other, but only while the post is open.</summary>
    public bool TryChangeState(ResponseState state, Post post, DateTimeOffset now)
    {
        if (!post.IsOpen(now)) return false;

        State = state;
        UpdatedAt = now;
        return true;
    }
}