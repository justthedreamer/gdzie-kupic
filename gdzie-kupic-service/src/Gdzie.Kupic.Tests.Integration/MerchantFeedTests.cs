using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class MerchantFeedTests : IntegrationTestBase
{
    private const string Url = "/api/merchant/feed";

    // One degree of latitude is ~111.2 km; branch sits at 50.0 / 19.0.
    private const double Near = 50.009;   // ~1 km
    private const double Middle = 50.045; // ~5 km
    private const double Far = 50.18;     // ~20 km

    private Category _category = null!;
    private Category _otherCategory = null!;
    private Tag _tag = null!;
    private Tag _otherTag = null!;
    private Guid _buyerId;
    private Guid _merchantId;
    private Guid _otherMerchantId;

    [SetUp]
    public async Task Seed()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _otherCategory = new Category(Guid.NewGuid(), "Video", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _otherTag = new Tag(Guid.NewGuid(), _otherCategory.Id, "Cameras", false, DateTimeOffset.UtcNow);

        _buyerId = await AuthenticateAsync(Role.Buyer);
        var otherUserId = await AuthenticateAsync(Role.Merchant);
        var userId = await AuthenticateAsync(Role.Merchant);

        _merchantId = Guid.NewGuid();
        _otherMerchantId = Guid.NewGuid();

        await WithDbAsync(async db =>
        {
            db.Categories.AddRange(_category, _otherCategory);
            db.Tags.AddRange(_tag, _otherTag);
            db.Merchants.AddRange(
                new Merchant(_merchantId, "Shop", null, DateTimeOffset.UtcNow),
                new Merchant(_otherMerchantId, "Other", null, DateTimeOffset.UtcNow));
            db.MerchantAccounts.AddRange(
                new MerchantAccount(Guid.NewGuid(), _merchantId, userId, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _otherMerchantId, otherUserId, DateTimeOffset.UtcNow));
            db.MerchantBranches.AddRange(
                new MerchantBranch(Guid.NewGuid(), _merchantId, "Main", new Coordinates(50.0, 19.0), null, null, null, DateTimeOffset.UtcNow),
                new MerchantBranch(Guid.NewGuid(), _otherMerchantId, "Main", new Coordinates(50.0, 19.0), null, null, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
    }

    private async Task<Guid> AddPostAsync(
        double lat = Near,
        DateTimeOffset? createdAt = null,
        bool urgent = false,
        Category? category = null,
        bool notified = true,
        bool notifyOther = false,
        decimal? radius = 5m,
        DateTimeOffset? expiresAt = null)
    {
        category ??= _category;
        var tag = category == _category ? _tag : _otherTag;
        var created = createdAt ?? DateTimeOffset.UtcNow.AddMinutes(-10);
        var deadline = urgent ? DateTimeOffset.UtcNow.AddHours(5) : (DateTimeOffset?)null;
        var post = new Post(Guid.NewGuid(), _buyerId, new Coordinates(lat, 19.0), radius, category.Id, tag.Id, "Post", null,
            deadline, expiresAt ?? DateTimeOffset.UtcNow.AddDays(3), created);

        await WithDbAsync(async db =>
        {
            db.Posts.Add(post);
            if (notified) db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), post.Id, _merchantId, created));
            if (notifyOther) db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), post.Id, _otherMerchantId, created));
            await db.SaveChangesAsync();
        });

        return post.Id;
    }

    private Task RespondAsync(Guid postId, ResponseState state) => WithDbAsync(async db =>
    {
        db.MerchantResponses.Add(new MerchantResponse(Guid.NewGuid(), postId, _merchantId, state, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    });

    private async Task<Feed.FeedPage> GetFeedAsync(string query = "")
    {
        var response = await Client.GetAsync($"{Url}{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsAsync<Feed.FeedPage>(response))!;
    }

    private static Guid[] Ids(Feed.FeedPage page) => page.Items.Select(i => i.Id).ToArray();

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Test]
    public async Task Anonymous_And_WrongRole_AreRejected()
    {
        using var anonymous = IntegrationTestSetup.Factory.CreateClient();
        (await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(Role.Buyer);
        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.GetAsync($"{Url}/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task MerchantWithoutOnboarding_GetsNotFound()
    {
        await AuthenticateAsync(Role.Merchant);

        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"{Url}/summary")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Feed_ListsOnlyActiveNonExpiredPostsTheMerchantWasNotifiedAbout()
    {
        var mine = await AddPostAsync();
        await AddPostAsync(notified: false, notifyOther: true);
        await AddPostAsync(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var closed = await AddPostAsync();
        await WithDbAsync(async db =>
        {
            (await db.Posts.SingleAsync(p => p.Id == closed)).TryClose(DateTimeOffset.UtcNow).ShouldBeTrue();
            await db.SaveChangesAsync();
        });

        var page = await GetFeedAsync("?tab=all");

        Ids(page).ShouldBe([mine]);
        page.NextCursor.ShouldBeNull();
    }

    [Test]
    public async Task Feed_ItemCarriesDistanceRadiusBuyerAndResponse()
    {
        var id = await AddPostAsync(Middle, radius: 8m, urgent: true);
        await RespondAsync(id, ResponseState.MayHaveIt);

        var item = (await GetFeedAsync("?tab=all")).Items.Single();

        item.Id.ShouldBe(id);
        item.DistanceKm.ShouldBe(5.0, 0.2);
        item.BuyerRadiusKm.ShouldBe(8m);
        item.BuyerName.ShouldNotBeNullOrWhiteSpace();
        item.IsUrgent.ShouldBeTrue();
        item.UrgentDeadline.ShouldNotBeNull();
        item.Category.Name.ShouldBe("Audio");
        item.Tag.Name.ShouldBe("Microphones");
        item.Status.ShouldBe("Active");
        item.MyResponse.ShouldBe("MayHaveIt");
    }

    [Test]
    public async Task Feed_ShowsTheBuyersFirstName_OrAPlaceholderWhenNoneIsSet()
    {
        var id = await AddPostAsync();

        (await GetFeedAsync("?tab=all")).Items.Single().BuyerName.ShouldBe("Kupuj\u0105cy");

        await WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == _buyerId)).FirstName = "Anna";
            await db.SaveChangesAsync();
        });

        var item = (await GetFeedAsync("?tab=all")).Items.Single();
        item.BuyerName.ShouldBe("Anna");

        var detail = await Client.GetAsync($"{Url}/{id}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await detail.Content.ReadAsStringAsync()).ShouldNotContain("@example.com");
    }

    [Test]
    public async Task Feed_DistanceIsMeasuredToTheNearestBranch()
    {
        await WithDbAsync(async db =>
        {
            db.MerchantBranches.Add(new MerchantBranch(Guid.NewGuid(), _merchantId, "Second", new Coordinates(Far, 19.0), null, null, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        await AddPostAsync(Far);

        (await GetFeedAsync()).Items.Single().DistanceKm.ShouldBeLessThan(1);
    }

    [Test]
    public async Task Tabs_SplitUnansweredFromAnswered_IncludingCantHelp()
    {
        var unanswered = await AddPostAsync();
        var positive = await AddPostAsync();
        var cantHelp = await AddPostAsync();
        await RespondAsync(positive, ResponseState.HaveIt);
        await RespondAsync(cantHelp, ResponseState.CantHelp);

        Ids(await GetFeedAsync()).ShouldBe([unanswered]);
        Ids(await GetFeedAsync("?tab=new")).ShouldBe([unanswered]);
        Ids(await GetFeedAsync("?tab=responded")).OrderBy(x => x).ShouldBe(new[] { positive, cantHelp }.OrderBy(x => x));
        (await GetFeedAsync("?tab=all")).Items.Count.ShouldBe(3);
    }

    [Test]
    public async Task Filters_CategoryAndMaxDistance_CanBeCombined()
    {
        var near = await AddPostAsync(Near);
        var middle = await AddPostAsync(Middle);
        await AddPostAsync(Far);
        var video = await AddPostAsync(Near, category: _otherCategory);

        Ids(await GetFeedAsync($"?categoryId={_otherCategory.Id}")).ShouldBe([video]);
        (await GetFeedAsync("?maxDistanceKm=10")).Items.Select(i => i.Id).OrderBy(x => x)
            .ShouldBe(new[] { near, middle, video }.OrderBy(x => x));
        Ids(await GetFeedAsync($"?categoryId={_category.Id}&maxDistanceKm=2")).ShouldBe([near]);
    }

    [Test]
    public async Task DefaultOrder_UrgentFirst_ThenNewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var oldPlain = await AddPostAsync(createdAt: now.AddHours(-5));
        var newPlain = await AddPostAsync(createdAt: now.AddHours(-1));
        var oldUrgent = await AddPostAsync(createdAt: now.AddHours(-4), urgent: true);
        var newUrgent = await AddPostAsync(createdAt: now.AddHours(-2), urgent: true);

        Ids(await GetFeedAsync()).ShouldBe([newUrgent, oldUrgent, newPlain, oldPlain]);
        Ids(await GetFeedAsync("?sort=newest")).ShouldBe([newUrgent, oldUrgent, newPlain, oldPlain]);
    }

    [Test]
    public async Task NearestOrder_SortsByDistance()
    {
        var far = await AddPostAsync(Far);
        var near = await AddPostAsync(Near);
        var middle = await AddPostAsync(Middle, urgent: true);

        Ids(await GetFeedAsync("?sort=nearest")).ShouldBe([near, middle, far]);
    }

    [TestCase("newest")]
    [TestCase("nearest")]
    public async Task Pagination_IsStableWithoutDuplicatesOrGaps_EvenWhenNewPostsArrive(string sort)
    {
        var now = DateTimeOffset.UtcNow;
        var all = new List<Guid>();
        for (var i = 0; i < 5; i++)
            all.Add(await AddPostAsync(50.0 + 0.003 * (i + 1), createdAt: now.AddMinutes(-10 - i), urgent: i == 1));

        var first = await GetFeedAsync($"?sort={sort}&limit=2");
        first.Items.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();

        // A new, nearest and newest post arrives between the page requests.
        await AddPostAsync(50.0001, createdAt: now);

        var second = await GetFeedAsync($"?sort={sort}&limit=2&cursor={first.NextCursor}");
        var third = await GetFeedAsync($"?sort={sort}&limit=2&cursor={second.NextCursor}");

        third.NextCursor.ShouldBeNull();
        var seen = Ids(first).Concat(Ids(second)).Concat(Ids(third)).ToList();
        seen.Count.ShouldBe(5);
        seen.Distinct().Count().ShouldBe(5);
        seen.ShouldBe(sort == "newest"
            ? [all[1], all[0], all[2], all[3], all[4]]
            : [all[0], all[1], all[2], all[3], all[4]]);
    }

    [Test]
    public async Task Limit_DefaultsTo20_AndIsCappedAt50()
    {
        for (var i = 0; i < 55; i++) await AddPostAsync(createdAt: DateTimeOffset.UtcNow.AddMinutes(-i - 1));

        var byDefault = await GetFeedAsync();
        byDefault.Items.Count.ShouldBe(20);
        byDefault.NextCursor.ShouldNotBeNull();

        var capped = await GetFeedAsync("?limit=500");
        capped.Items.Count.ShouldBe(50);
        capped.NextCursor.ShouldNotBeNull();

        var rest = await GetFeedAsync($"?limit=50&cursor={capped.NextCursor}");
        rest.Items.Count.ShouldBe(5);
        rest.NextCursor.ShouldBeNull();
    }

    [TestCase("?tab=nope")]
    [TestCase("?sort=nope")]
    [TestCase("?limit=0")]
    [TestCase("?maxDistanceKm=-1")]
    [TestCase("?cursor=not-a-cursor")]
    public async Task InvalidQuery_ReturnsBadRequest(string query) =>
        (await Client.GetAsync($"{Url}{query}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Test]
    public async Task Summary_CountsUnansweredAndAnsweredActivePosts()
    {
        await AddPostAsync();
        await AddPostAsync();
        var answered = await AddPostAsync();
        var cantHelp = await AddPostAsync();
        await AddPostAsync(notified: false, notifyOther: true);
        await AddPostAsync(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await RespondAsync(answered, ResponseState.HaveIt);
        await RespondAsync(cantHelp, ResponseState.CantHelp);

        var summary = (await ReadAsAsync<Feed.Summary>(await Client.GetAsync($"{Url}/summary")))!;

        summary.NewCount.ShouldBe(2);
        summary.RespondedCount.ShouldBe(2);
    }

    [Test]
    public async Task Detail_ReturnsEndedPostWithResponseAndThread()
    {
        var id = await AddPostAsync(Middle);
        var threadId = Guid.NewGuid();
        await RespondAsync(id, ResponseState.CanOrderIt);
        await WithDbAsync(async db =>
        {
            db.ChatThreads.Add(new ChatThread(threadId, id, _merchantId, false, DateTimeOffset.UtcNow));
            (await db.Posts.SingleAsync(p => p.Id == id)).TryFulfil(DateTimeOffset.UtcNow).ShouldBeTrue();
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"{Url}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await ReadAsAsync<Feed.FeedDetail>(response))!;
        detail.Id.ShouldBe(id);
        detail.Status.ShouldBe("Fulfilled");
        detail.MyResponse.ShouldBe("CanOrderIt");
        detail.ThreadId.ShouldBe(threadId);
        detail.DistanceKm.ShouldBe(5.0, 0.2);
    }

    [Test]
    public async Task Detail_WithoutThread_HasNullThreadId()
    {
        var id = await AddPostAsync();

        var detail = (await ReadAsAsync<Feed.FeedDetail>(await Client.GetAsync($"{Url}/{id}")))!;

        detail.ThreadId.ShouldBeNull();
        detail.MyResponse.ShouldBeNull();
    }

    [Test]
    public async Task Detail_NotNotifiedOrUnknown_ReturnsNotFound()
    {
        var foreign = await AddPostAsync(notified: false, notifyOther: true);

        (await Client.GetAsync($"{Url}/{foreign}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}