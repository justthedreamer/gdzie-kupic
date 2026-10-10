using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Common;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class NewMerchantScanTests : IntegrationTestBase
{
    private const double MerchantLat = 50.06;
    private const double MerchantLon = 19.94;

    private Category _category = null!;
    private Tag _tag = null!;
    private Category _otherCategory = null!;
    private Tag _otherCategoryTag = null!;
    private Guid _merchantUserId;

    private RecordingJobScheduler Scheduler => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingJobScheduler>();
    private RecordingNotificationDispatcher Dispatcher => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingNotificationDispatcher>();

    [SetUp]
    public async Task SeedCatalogue()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _otherCategory = new Category(Guid.NewGuid(), "Video", false, DateTimeOffset.UtcNow);
        _otherCategoryTag = new Tag(Guid.NewGuid(), _otherCategory.Id, "Cameras", false, DateTimeOffset.UtcNow);

        await WithDbAsync(async db =>
        {
            db.Categories.AddRange(_category, _otherCategory);
            db.Tags.AddRange(_tag, _otherCategoryTag);
            await db.SaveChangesAsync();
        });
    }

    [Test]
    public async Task Onboarding_WritesOutboxEntryForTheNewMerchant()
    {
        var merchantId = await OnboardMerchantAsync();

        await WithDbAsync(async db =>
        {
            var entry = await db.Outbox.SingleAsync();
            entry.Type.ShouldBe("NotifyNewMerchant");
            entry.Payload.ShouldContain(merchantId.ToString());
            entry.ProcessedAt.ShouldBeNull();
        });
    }

    [Test]
    public async Task AddingSubscription_WritesOutboxEntry()
    {
        var merchantId = await OnboardMerchantAsync();

        (await Client.PostAsJsonAsync("/api/merchant/subscriptions", new Subscriptions.Request(_category.Id, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        await WithDbAsync(async db =>
        {
            var entries = await db.Outbox.Where(o => o.Type == "NotifyNewMerchant").ToListAsync();
            entries.Count.ShouldBe(2);
            entries.ShouldAllBe(e => e.Payload.Contains(merchantId.ToString()));
        });
    }

    [Test]
    public async Task DuplicateSubscription_WritesNoExtraOutboxEntry()
    {
        await OnboardMerchantAsync();
        var request = new Subscriptions.Request(_category.Id, null);

        await Client.PostAsJsonAsync("/api/merchant/subscriptions", request);
        (await Client.PostAsJsonAsync("/api/merchant/subscriptions", request)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await WithDbAsync(async db => (await db.Outbox.CountAsync()).ShouldBe(2));
    }

    [Test]
    public async Task Scan_NotifiesAboutMatchingOpenPosts_IncludingPendingAndUnlimitedRadius()
    {
        var pending = await AddPostAsync(PostSpec.Default);
        var unlimited = await AddPostAsync(PostSpec.Default with { RadiusKm = null, Latitude = 10, Longitude = 10 });
        var exactTag = await AddPostAsync(PostSpec.Default with { TagId = _tag.Id });
        var merchantId = await OnboardAndSubscribeAsync();

        await ProcessAsync();

        (await NotifiedPostsAsync(merchantId)).ShouldBe([pending, unlimited, exactTag], ignoreOrder: true);
        Dispatcher.Dispatched.Count.ShouldBe(3);
        Dispatcher.Dispatched.ShouldAllBe(n => n.Kind == Gdzie.Kupic.Notifications.NotificationKind.NewPost);
    }

    [Test]
    public async Task Scan_SkipsNonMatchingExpiredEndedAndOwnPosts()
    {
        var matching = await AddPostAsync(PostSpec.Default);
        await AddPostAsync(PostSpec.Default with { CategoryId = _otherCategory.Id, TagId = _otherCategoryTag.Id });
        await AddPostAsync(PostSpec.Default with { Latitude = 51.5 });
        await AddPostAsync(PostSpec.Default with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) });
        await AddPostAsync(PostSpec.Default with { Ended = PostStatus.Closed });
        await AddPostAsync(PostSpec.Default with { Ended = PostStatus.Fulfilled });
        await AddPostAsync(PostSpec.Default with { Ended = PostStatus.Expired });
        var merchantId = await OnboardAndSubscribeAsync();
        await AddPostAsync(PostSpec.Default with { BuyerId = _merchantUserId });

        await ProcessAsync();

        (await NotifiedPostsAsync(merchantId)).ShouldBe([matching]);
    }

    [Test]
    public async Task Scan_SkipsPostsTheMerchantWasAlreadyNotifiedAbout()
    {
        var alreadyNotified = await AddPostAsync(PostSpec.Default);
        var fresh = await AddPostAsync(PostSpec.Default);
        var merchantId = await OnboardAndSubscribeAsync();
        await WithDbAsync(async db =>
        {
            db.PostNotifications.Add(new(Guid.NewGuid(), alreadyNotified, merchantId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });

        await ProcessAsync();

        (await NotifiedPostsAsync(merchantId)).ShouldBe([alreadyNotified, fresh], ignoreOrder: true);
        Dispatcher.Dispatched.Select(d => d.PostId).ShouldBe([fresh]);
    }

    [Test]
    public async Task Scan_DoesNotNotifyBannedMerchant()
    {
        await AddPostAsync(PostSpec.Default);
        var merchantId = await OnboardAndSubscribeAsync();
        await WithDbAsync(async db =>
        {
            (await db.Merchants.SingleAsync(m => m.Id == merchantId)).BanDetails = new BanDetails(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        await ProcessAsync();

        (await NotifiedPostsAsync(merchantId)).ShouldBeEmpty();
    }

    [Test]
    public async Task Scan_RunTwice_CreatesNoDuplicates()
    {
        await AddPostAsync(PostSpec.Default);
        var merchantId = await OnboardAndSubscribeAsync();
        await ProcessAsync();

        await RunScanAsync(merchantId);

        (await NotifiedPostsAsync(merchantId)).Count.ShouldBe(1);
        Dispatcher.Dispatched.Count.ShouldBe(1);
    }

    [Test]
    public async Task Scan_WithoutSubscriptions_NotifiesNothing()
    {
        await AddPostAsync(PostSpec.Default);
        var merchantId = await OnboardMerchantAsync();

        await ProcessAsync();

        (await NotifiedPostsAsync(merchantId)).ShouldBeEmpty();
    }

    private sealed record PostSpec(
        double Latitude, double Longitude, decimal? RadiusKm, Guid CategoryId, Guid TagId,
        DateTimeOffset ExpiresAt, PostStatus? Ended, Guid? BuyerId)
    {
        public static readonly PostSpec Default = new(
            MerchantLat, MerchantLon, 5m, Guid.Empty, Guid.Empty, DateTimeOffset.UtcNow.AddDays(1), null, null);
    }

    private async Task<Guid> AddPostAsync(PostSpec spec)
    {
        var id = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            var categoryId = spec.CategoryId == Guid.Empty ? _category.Id : spec.CategoryId;
            var tagId = spec.TagId == Guid.Empty ? _tag.Id : spec.TagId;
            var now = DateTimeOffset.UtcNow;
            var post = new Post(
                id, spec.BuyerId ?? Guid.NewGuid(), new Coordinates(spec.Latitude, spec.Longitude), spec.RadiusKm,
                categoryId, tagId, "Need", null, null, spec.ExpiresAt, now);

            if (spec.Ended == PostStatus.Closed) post.TryClose(now);
            if (spec.Ended == PostStatus.Fulfilled) post.TryFulfil(now);
            if (spec.Ended == PostStatus.Expired) post.TryExpire(spec.ExpiresAt);
            if (spec.Ended == PostStatus.Expired && post.Status != PostStatus.Expired)
                db.Entry(post).Property(p => p.Status).CurrentValue = PostStatus.Expired;

            db.Posts.Add(post);
            await db.SaveChangesAsync();
        });
        return id;
    }

    private async Task<Guid> OnboardMerchantAsync()
    {
        _merchantUserId = await AuthenticateAsync(Role.Merchant);
        var response = await Client.PostAsJsonAsync("/api/merchant/onboarding",
            new Onboarding.Request("Shop", null, new Onboarding.BranchRequest("Main", null, null, MerchantLat, MerchantLon, null)));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var merchantId = Guid.Empty;
        await WithDbAsync(async db => merchantId = (await db.Merchants.SingleAsync()).Id);
        return merchantId;
    }

    private async Task<Guid> OnboardAndSubscribeAsync()
    {
        var merchantId = await OnboardMerchantAsync();
        (await Client.PostAsJsonAsync("/api/merchant/subscriptions", new Subscriptions.Request(_category.Id, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        return merchantId;
    }

    private async Task ProcessAsync()
    {
        using (var scope = IntegrationTestSetup.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<OutboxRelayJob>().RunAsync();
        await Scheduler.RunPendingAsync();
    }

    private async Task RunScanAsync(Guid merchantId)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotifyNewMerchantJob>().RunAsync(merchantId);
    }

    private async Task<List<Guid>> NotifiedPostsAsync(Guid merchantId)
    {
        var result = new List<Guid>();
        await WithDbAsync(async db =>
            result.AddRange(await db.PostNotifications.Where(n => n.MerchantId == merchantId).Select(n => n.PostId).ToListAsync()));
        return result;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
