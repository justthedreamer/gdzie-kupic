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

    Task<IReadOnlyList<Post>> ListBuyerPostsAsync(Guid buyerId, bool active, int limit, CancellationToken ct = default);

    /// <summary>Moves overdue Active posts to Expired; returns how many were changed.</summary>
    Task<int> ExpireOverduePostsAsync(DateTimeOffset now, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
