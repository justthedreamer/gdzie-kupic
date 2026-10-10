using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Common;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Posts;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class MerchantMatchingTests : IntegrationTestBase
{
    private const double PostLat = 50.0;
    private const double PostLon = 19.0;

    // One degree of latitude is ~111.2 km.
    private const double Inside = 0.036;            // ~4 km
    private const double InsideTolerance = 0.063;   // ~7 km, beyond 5 km but within 5 km + 3 km
    private const double Outside = 0.081;           // ~9 km

    private Category _category = null!;
    private Tag _tag = null!;
    private Tag _otherTag = null!;
    private Guid _buyerId;

    private RecordingJobScheduler Scheduler => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingJobScheduler>();

    [SetUp]
    public async Task SeedCatalogue()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _otherTag = new Tag(Guid.NewGuid(), _category.Id, "Speakers", false, DateTimeOffset.UtcNow);

        await WithDbAsync(async db =>
        {
            db.Categories.Add(_category);
            db.Tags.AddRange(_tag, _otherTag);
            await db.SaveChangesAsync();
        });

        _buyerId = await AuthenticateAsync(Role.Buyer);
    }

    [Test]
    public async Task Relay_PicksUpCreatedPost_AndMarksEntryProcessedExactlyOnce()
    {
        var post = await CreatePostAsync(5m);

        (await RunRelayAsync()).ShouldBe(1);
        (await RunRelayAsync()).ShouldBe(0);

        Scheduler.CountOf<NotifyMerchantsJob>().ShouldBe(1);
        await WithDbAsync(async db =>
        {
            var entry = await db.Outbox.SingleAsync();
            entry.ProcessedAt.ShouldNotBeNull();
            entry.Payload.ShouldContain(post.Id.ToString());
        });
    }

    [Test]
    public async Task RadiusEdges_InsideAndInsideToleranceMatch_OutsideDoesNot()
    {
        var inside = await AddMerchantAsync(Inside, subscribeToCategory: true);
        var tolerance = await AddMerchantAsync(InsideTolerance, subscribeToCategory: true);
        var outside = await AddMerchantAsync(Outside, subscribeToCategory: true);

        var post = await CreatePostAsync(5m);
        await ProcessAsync();

        (await NotifiedMerchantsAsync(post.Id)).ShouldBe([inside, tolerance], ignoreOrder: true);
        (await NotifiedMerchantsAsync(post.Id)).ShouldNotContain(outside);
    }

    [Test]
    public async Task AnyBranchWithinReach_Matches()
    {
        var merchant = await AddMerchantAsync(Outside, subscribeToCategory: true);
        await WithDbAsync(async db =>
        {
            db.MerchantBranches.Add(new MerchantBranch(
                Guid.NewGuid(), merchant, "Near", new Coordinates(PostLat + Inside, PostLon), null, null, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });

        var post = await CreatePostAsync(5m);
        await ProcessAsync();

        (await NotifiedMerchantsAsync(post.Id)).ShouldBe([merchant]);
    }

    [Test]
    public async Task SubscriptionLevels_CategoryAndExactTagMatch_OtherTagDoesNot()
    {
        var categoryLevel = await AddMerchantAsync(Inside, subscribeToCategory: true);
        var exactTag = await AddMerchantAsync(Inside, tag: _tag);
        var otherTag = await AddMerchantAsync(Inside, tag: _otherTag);
        await AddMerchantAsync(Inside);

        var post = await CreatePostAsync(5m);
        await ProcessAsync();

        (await NotifiedMerchantsAsync(post.Id)).ShouldBe([categoryLevel, exactTag], ignoreOrder: true);
        (await NotifiedMerchantsAsync(post.Id)).ShouldNotContain(otherTag);
    }

    [Test]
    public async Task UnlimitedRadius_MatchesRegardlessOfDistance()
    {
        var far = await AddMerchantAsync(30, subscribeToCategory: true);
        var unsubscribed = await AddMerchantAsync(Inside);

        var post = await CreatePostAsync(null);
        await ProcessAsync();

        (await NotifiedMerchantsAsync(post.Id)).ShouldBe([far]);
        (await NotifiedMerchantsAsync(post.Id)).ShouldNotContain(unsubscribed);
    }

    [Test]
    public async Task BannedMerchantsAndThePostAuthor_AreNeverMatched()
    {
        var banned = await AddMerchantAsync(Inside, subscribeToCategory: true, banned: true);
        var author = await AddMerchantAsync(Inside, subscribeToCategory: true, accountUserId: _buyerId);
        var regular = await AddMerchantAsync(Inside, subscribeToCategory: true);

        var post = await CreatePostAsync(5m);
        await ProcessAsync();

        var notified = await NotifiedMerchantsAsync(post.Id);
        notified.ShouldBe([regular]);
        notified.ShouldNotContain(banned);
        notified.ShouldNotContain(author);
    }

    [Test]
    public async Task MoreThanFiftyMatches_AreProcessedInBatchesOfFifty()
    {
        for (var i = 0; i < 120; i++)
            await AddMerchantAsync(Inside, subscribeToCategory: true);

        var post = await CreatePostAsync(5m);
        await ProcessAsync();

        Scheduler.CountOf<NotifyMerchantsBatchJob>().ShouldBe(3);
        (await NotifiedMerchantsAsync(post.Id)).Count.ShouldBe(120);
    }

    [Test]
    public async Task RerunningTheJob_CreatesNoDuplicateNotifications()
    {
        await AddMerchantAsync(Inside, subscribeToCategory: true);
        await AddMerchantAsync(Inside, subscribeToCategory: true);
        var post = await CreatePostAsync(5m);
        await ProcessAsync();

        Scheduler.Reset();
        using (var scope = IntegrationTestSetup.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NotifyMerchantsJob>().RunAsync(post.Id);
        await Scheduler.RunPendingAsync();

        (await NotifiedMerchantsAsync(post.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task ZeroMatches_MarksPostDispatchedImmediately()
    {
        var post = await CreatePostAsync(5m);

        await RunRelayAsync();
        await RunPostMatchingJobOnlyAsync();

        Scheduler.CountOf<NotifyMerchantsBatchJob>().ShouldBe(0);
        var fetched = (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"/api/posts/{post.Id}"))!;
        fetched.NotificationDispatchStatus.ShouldBe("Dispatched");
        fetched.NotifiedCount.ShouldBe(0);
    }

    [Test]
    public async Task PostStaysPendingUntilMatchingJobRuns_ThenBecomesDispatchedWithCount()
    {
        await AddMerchantAsync(Inside, subscribeToCategory: true);
        var post = await CreatePostAsync(5m);

        (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"/api/posts/{post.Id}"))!
            .NotificationDispatchStatus.ShouldBe("Pending");

        await ProcessAsync();

        var fetched = (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"/api/posts/{post.Id}"))!;
        fetched.NotificationDispatchStatus.ShouldBe("Dispatched");
        fetched.NotifiedCount.ShouldBe(1);
    }

    private async Task<Posts.PostWithCountDto> CreatePostAsync(decimal? radius)
    {
        var response = await Client.PostAsJsonAsync("/api/posts",
            new Posts.CreateRequest(PostLat, PostLon, radius, _category.Id, _tag.Id, "Need a microphone", null, null));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadAsAsync<Posts.PostWithCountDto>(response))!;
    }

    private async Task<int> RunRelayAsync()
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxRelayJob>().RunAsync();
    }

    private async Task ProcessAsync()
    {
        await RunRelayAsync();
        await Scheduler.RunPendingAsync();
    }

    private Task RunPostMatchingJobOnlyAsync() => Scheduler.RunPendingAsync();

    private async Task<Guid> AddMerchantAsync(
        double latitudeOffset,
        bool subscribeToCategory = false,
        Tag? tag = null,
        bool banned = false,
        Guid? accountUserId = null)
    {
        var merchantId = Guid.NewGuid();

        await WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var merchant = new Merchant(merchantId, "Shop", null, now);
            if (banned) merchant.BanDetails = new BanDetails(now);
            db.Merchants.Add(merchant);
            db.MerchantBranches.Add(new MerchantBranch(
                Guid.NewGuid(), merchantId, "Main", new Coordinates(PostLat + latitudeOffset, PostLon), null, null, null, now));

            if (accountUserId is not null)
                db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), merchantId, accountUserId.Value, now));

            if (subscribeToCategory)
                db.MerchantSubscriptions.Add(new MerchantSubscription(Guid.NewGuid(), merchantId, _category.Id, null, now));
            if (tag is not null)
                db.MerchantSubscriptions.Add(new MerchantSubscription(Guid.NewGuid(), merchantId, _category.Id, tag.Id, now));

            await db.SaveChangesAsync();
        });

        return merchantId;
    }

    private async Task<List<Guid>> NotifiedMerchantsAsync(Guid postId)
    {
        var result = new List<Guid>();
        await WithDbAsync(async db =>
            result.AddRange(await db.PostNotifications.Where(n => n.PostId == postId).Select(n => n.MerchantId).ToListAsync()));
        return result;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
