namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum SubscriptionError
{
    None,
    Validation,
    Duplicate,
    NotFound,
    NotOnboarded
}

public sealed record SubscriptionResult<T>(T? Value, SubscriptionError Error, string? Message = null)
{
    public bool IsSuccess => Error == SubscriptionError.None;
}

public interface ISubscriptionService
{
    /// <summary>Lists all subscriptions, including those pointing to since-disabled categories or tags.</summary>
    Task<SubscriptionResult<IReadOnlyList<MerchantSubscription>>> ListAsync(Guid userId, CancellationToken ct = default);

    Task<SubscriptionResult<MerchantSubscription>> AddAsync(Guid userId, Guid categoryId, Guid? tagId, CancellationToken ct = default);

    /// <summary>NotFound also covers subscriptions that belong to another merchant.</summary>
    Task<SubscriptionResult<bool>> RemoveAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default);
}