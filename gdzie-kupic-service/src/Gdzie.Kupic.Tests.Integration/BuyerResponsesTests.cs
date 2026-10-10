using System.Net;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Service.API.Contract.Posts;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class BuyerResponsesTests : IntegrationTestBase
{
    private Guid _buyerId;
    private Guid _postId;

    [SetUp]
    public async Task Seed()
    {
        var category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        var tag = new Tag(Guid.NewGuid(), category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _buyerId = await AuthenticateAsync(Role.Buyer);
        _postId = Guid.NewGuid();

        await WithDbAsync(async db =>
        {
            db.Categories.Add(category);
            db.Tags.Add(tag);
            db.Posts.Add(new Post(_postId, _buyerId, new Coordinates(50, 19), 5m, category.Id, tag.Id, "Need a mic", null,
                null, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddHours(-2)));
            await db.SaveChangesAsync();
        });
    }

    private async Task<Guid> AddMerchantAsync(string name, ResponseState? state, DateTimeOffset? at = null, bool notified = true, bool thread = false)
    {
        var merchantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Merchants.Add(new Merchant(merchantId, name, null, DateTimeOffset.UtcNow));
            if (notified) db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), _postId, merchantId, DateTimeOffset.UtcNow));
            if (state is { } s) db.MerchantResponses.Add(new MerchantResponse(Guid.NewGuid(), _postId, merchantId, s, at ?? DateTimeOffset.UtcNow));
            if (thread) db.ChatThreads.Add(new ChatThread(threadId, _postId, merchantId, false, DateTimeOffset.UtcNow.AddHours(-1)));
            await db.SaveChangesAsync();
        });
        return merchantId;
    }

    private async Task ChangeStateAsync(Guid merchantId, ResponseState state) => await WithDbAsync(async db =>
    {
        var post = await db.Posts.SingleAsync();
        var response = await db.MerchantResponses.SingleAsync(r => r.MerchantId == merchantId);
        response.TryChangeState(state, post, DateTimeOffset.UtcNow).ShouldBeTrue();
        await db.SaveChangesAsync();
    });

    private async Task<Posts.StatusDto> StatusAsync()
    {
        var response = await Client.GetAsync($"/api/posts/{_postId}/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsAsync<Posts.StatusDto>(response))!;
    }

    private async Task<List<Posts.ResponseItem>> ResponsesAsync()
    {
        var response = await Client.GetAsync($"/api/posts/{_postId}/responses");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsAsync<List<Posts.ResponseItem>>(response))!;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Test]
    public async Task Status_WithoutResponses_EveryNotifiedMerchantIsChecking()
    {
        await AddMerchantAsync("A", null);
        await AddMerchantAsync("B", null);

        var status = await StatusAsync();

        status.NotifiedCount.ShouldBe(2);
        status.CheckingCount.ShouldBe(2);
        (status.HaveItCount + status.MayHaveItCount + status.CanOrderItCount + status.CannotHelpCount).ShouldBe(0);
    }

    [Test]
    public async Task Status_CountsEachStateAndSubtractsRespondedFromChecking()
    {
        await AddMerchantAsync("A", ResponseState.HaveIt);
        await AddMerchantAsync("B", ResponseState.HaveIt);
        await AddMerchantAsync("C", ResponseState.MayHaveIt);
        await AddMerchantAsync("D", ResponseState.CanOrderIt);
        await AddMerchantAsync("E", ResponseState.CantHelp);
        await AddMerchantAsync("F", null);

        var status = await StatusAsync();

        status.NotifiedCount.ShouldBe(6);
        status.HaveItCount.ShouldBe(2);
        status.MayHaveItCount.ShouldBe(1);
        status.CanOrderItCount.ShouldBe(1);
        status.CannotHelpCount.ShouldBe(1);
        status.CheckingCount.ShouldBe(1);
    }

    [Test]
    public async Task Status_ChangedStateMovesTheCount()
    {
        var merchant = await AddMerchantAsync("A", ResponseState.MayHaveIt);
        (await StatusAsync()).MayHaveItCount.ShouldBe(1);

        await ChangeStateAsync(merchant, ResponseState.HaveIt);

        var status = await StatusAsync();
        status.MayHaveItCount.ShouldBe(0);
        status.HaveItCount.ShouldBe(1);
        status.CheckingCount.ShouldBe(0);
    }

    [Test]
    public async Task Status_CheckingNeverGoesNegative()
    {
        await AddMerchantAsync("Unnotified", ResponseState.HaveIt, notified: false);

        var status = await StatusAsync();

        status.NotifiedCount.ShouldBe(0);
        status.CheckingCount.ShouldBe(0);
    }

    [Test]
    public async Task Responses_ListsOnlyPositiveOnes_NewestUpdateFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var oldest = await AddMerchantAsync("Oldest", ResponseState.MayHaveIt, now.AddMinutes(-30));
        await AddMerchantAsync("Cant", ResponseState.CantHelp, now.AddMinutes(-5));
        var newest = await AddMerchantAsync("Newest", ResponseState.HaveIt, now.AddMinutes(-10));
        await AddMerchantAsync("Silent", null);

        var items = await ResponsesAsync();

        items.Select(i => i.MerchantId).ShouldBe([newest, oldest]);
        items[0].ShopName.ShouldBe("Newest");
        items[0].State.ShouldBe("HaveIt");
        items[1].State.ShouldBe("MayHaveIt");
        items[0].ThreadId.ShouldBeNull();
        items[0].UnreadCount.ShouldBe(0);
    }

    [Test]
    public async Task Responses_StateChangeReordersAndCantHelpDropsOut()
    {
        var now = DateTimeOffset.UtcNow;
        var a = await AddMerchantAsync("A", ResponseState.HaveIt, now.AddMinutes(-20));
        var b = await AddMerchantAsync("B", ResponseState.MayHaveIt, now.AddMinutes(-10));

        await ChangeStateAsync(a, ResponseState.CanOrderIt);
        (await ResponsesAsync()).Select(i => i.MerchantId).ShouldBe([a, b]);

        await ChangeStateAsync(b, ResponseState.CantHelp);
        (await ResponsesAsync()).Select(i => i.MerchantId).ShouldBe([a]);
        (await StatusAsync()).CannotHelpCount.ShouldBe(1);
    }

    [Test]
    public async Task Responses_CarryThreadIdAndTheBuyersUnreadCount()
    {
        var merchantId = await AddMerchantAsync("Shop", ResponseState.HaveIt, thread: true);
        var thread = Guid.Empty;
        await WithDbAsync(async db =>
        {
            thread = (await db.ChatThreads.SingleAsync()).Id;
            var merchantUser = Guid.NewGuid();
            db.ChatMessages.Add(new ChatMessage(Guid.NewGuid(), thread, _buyerId, "mine", null, DateTimeOffset.UtcNow.AddMinutes(-3)));
            db.ChatMessages.Add(new ChatMessage(Guid.NewGuid(), thread, merchantUser, "one", null, DateTimeOffset.UtcNow.AddMinutes(-2)));
            db.ChatMessages.Add(new ChatMessage(Guid.NewGuid(), thread, merchantUser, "two", null, DateTimeOffset.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
        });

        var item = (await ResponsesAsync()).Single();
        item.MerchantId.ShouldBe(merchantId);
        item.ThreadId.ShouldBe(thread);
        item.UnreadCount.ShouldBe(2);

        await WithDbAsync(async db =>
        {
            (await db.ChatThreads.SingleAsync()).BuyerLastReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        (await ResponsesAsync()).Single().UnreadCount.ShouldBe(0);
    }

    [Test]
    public async Task Responses_CantHelpThreadStaysOutOfTheList()
    {
        await AddMerchantAsync("Shop", ResponseState.CantHelp, thread: true);

        (await ResponsesAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task BothEndpoints_ForeignOrUnknownPost_ReturnNotFound()
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.GetAsync($"/api/posts/{_postId}/status")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/posts/{_postId}/responses")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/posts/{Guid.NewGuid()}/responses")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Responses_RequireBuyerRole()
    {
        using var anonymous = IntegrationTestSetup.Factory.CreateClient();
        (await anonymous.GetAsync($"/api/posts/{_postId}/responses")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(Role.Merchant);
        (await Client.GetAsync($"/api/posts/{_postId}/responses")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}