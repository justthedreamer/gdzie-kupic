namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Infrastructure;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Storage;

internal sealed class SubscriptionService(
    IMarketplaceStorage marketplace,
    ICatalogueStorage catalogue) : ISubscriptionService
{
    public async Task<SubscriptionResult<IReadOnlyList<MerchantSubscription>>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return Fail<IReadOnlyList<MerchantSubscription>>(SubscriptionError.NotOnboarded, NotOnboardedMessage);

        var items = await marketplace.GetSubscriptionsAsync(merchantId.Value, ct);
        return new SubscriptionResult<IReadOnlyList<MerchantSubscription>>(items, SubscriptionError.None);
    }

    public async Task<SubscriptionResult<MerchantSubscription>> AddAsync(
        Guid userId, Guid categoryId, Guid? tagId, CancellationToken ct = default)
    {
        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return Fail<MerchantSubscription>(SubscriptionError.NotOnboarded, NotOnboardedMessage);

        var category = await catalogue.FindCategoryAsync(categoryId, ct);
        if (category is null) return Fail<MerchantSubscription>(SubscriptionError.Validation, "Category does not exist.");
        if (category.IsDisabled) return Fail<MerchantSubscription>(SubscriptionError.Validation, "Category is disabled.");

        if (tagId is not null)
        {
            var tag = await catalogue.FindTagAsync(tagId.Value, ct);
            if (tag is null) return Fail<MerchantSubscription>(SubscriptionError.Validation, "Tag does not exist.");
            if (tag.CategoryId != categoryId) return Fail<MerchantSubscription>(SubscriptionError.Validation, "Tag does not belong to the category.");
            if (tag.IsDisabled) return Fail<MerchantSubscription>(SubscriptionError.Validation, "Tag is disabled.");
        }

        var subscription = new MerchantSubscription(Guid.NewGuid(), merchantId.Value, categoryId, tagId, DateTimeOffset.UtcNow);

        return await marketplace.TryAddSubscriptionAsync(subscription, OutboxMessages.NotifyNewMerchant(merchantId.Value, subscription.CreatedAt), ct)
            ? new SubscriptionResult<MerchantSubscription>(subscription, SubscriptionError.None)
            : Fail<MerchantSubscription>(SubscriptionError.Duplicate, "The merchant is already subscribed to this category/tag.");
    }

    public async Task<SubscriptionResult<bool>> RemoveAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default)
    {
        var merchantId = await marketplace.FindMerchantIdByUserIdAsync(userId, ct);
        if (merchantId is null) return Fail<bool>(SubscriptionError.NotOnboarded, NotOnboardedMessage);

        return await marketplace.DeleteSubscriptionAsync(merchantId.Value, subscriptionId, ct)
            ? new SubscriptionResult<bool>(true, SubscriptionError.None)
            : Fail<bool>(SubscriptionError.NotFound, "Subscription not found.");
    }

    private const string NotOnboardedMessage = "The merchant has not completed onboarding.";

    private static SubscriptionResult<T> Fail<T>(SubscriptionError error, string message) => new(default, error, message);
}