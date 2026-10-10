namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Marketplace;

public interface IPostStorage
{
    /// <summary>Persists the post and its outbox entry in a single save (one transaction).</summary>
    Task AddWithOutboxAsync(Post post, Domain.Model.Infrastructure.OutboxMessage outboxMessage, CancellationToken ct = default);

    /// <summary>Read-only post including category and tag; null when it does not belong to the buyer.</summary>
    Task<Post?> FindBuyerPostAsync(Guid postId, Guid buyerId, CancellationToken ct = default);

    /// <summary>Tracked post for modification; call <see cref="SaveChangesAsync"/> afterwards.</summary>
    Task<Post?> FindBuyerPostForUpdateAsync(Guid postId, Guid buyerId, CancellationToken ct = default);

    /// <summary>Tracked post regardless of owner, for background jobs.</summary>
    Task<Post?> FindForUpdateAsync(Guid postId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, int>> GetNotifiedCountsAsync(IReadOnlyCollection<Guid> postIds, CancellationToken ct = default);

    Task<IReadOnlyList<Post>> ListBuyerPostsAsync(Guid buyerId, bool active, int limit, CancellationToken ct = default);

    /// <summary>Moves overdue Active posts to Expired; returns the ids of the posts that were changed.</summary>
    Task<IReadOnlyList<Guid>> ExpireOverduePostsAsync(DateTimeOffset now, CancellationToken ct = default);

    Task<Guid?> FindOwnerIdAsync(Guid postId, CancellationToken ct = default);

    /// <summary>User ids of every account of every merchant that has a notification row for the post.</summary>
    Task<IReadOnlyList<Guid>> FindNotifiedUserIdsAsync(Guid postId, CancellationToken ct = default);

    /// <summary>User ids of every account of the merchant.</summary>
    Task<IReadOnlyList<Guid>> FindMerchantUserIdsAsync(Guid merchantId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
