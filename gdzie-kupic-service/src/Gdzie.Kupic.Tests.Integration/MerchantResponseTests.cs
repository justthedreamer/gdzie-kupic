using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class MerchantResponseTests : IntegrationTestBase
{
    private Guid _merchantId;
    private Guid _postId;
    private Guid _buyerId;

    [SetUp]
    public async Task Seed()
    {
        var category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        var tag = new Tag(Guid.NewGuid(), category.Id, "Microphones", false, DateTimeOffset.UtcNow);

        _buyerId = await AuthenticateAsync(Role.Buyer);
        var merchantUserId = await AuthenticateAsync(Role.Merchant);

        _merchantId = Guid.NewGuid();
        _postId = Guid.NewGuid();

        await WithDbAsync(async db =>
        {
            db.Categories.Add(category);
            db.Tags.Add(tag);
            db.Merchants.Add(new Merchant(_merchantId, "Shop", null, DateTimeOffset.UtcNow));
            db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), _merchantId, merchantUserId, DateTimeOffset.UtcNow));
            db.Posts.Add(new Post(_postId, _buyerId, new Coordinates(50, 19), 5m, category.Id, tag.Id, "Need a mic", null,
                null, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow));
            db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), _postId, _merchantId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
    }

    private string Url(Guid? postId = null) => $"/api/merchant/feed/{postId ?? _postId}/response";

    private Task<HttpResponseMessage> RespondAsync(string state, Guid? postId = null) =>
        Client.PutAsJsonAsync(Url(postId), new Feed.RespondRequest(state));

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Test]
    public async Task Anonymous_And_WrongRole_AreRejected()
    {
        var anonymous = IntegrationTestSetup.Factory.CreateClient();
        (await anonymous.PutAsJsonAsync(Url(), new Feed.RespondRequest("HaveIt"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(Role.Buyer);
        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase("")]
    [TestCase("Maybe")]
    [TestCase("haveit")]
    [TestCase("42")]
    public async Task InvalidState_ReturnsBadRequest(string state) =>
        (await RespondAsync(state)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [TestCase("MayHaveIt")]
    [TestCase("HaveIt")]
    [TestCase("CanOrderIt")]
    public async Task FirstPositiveResponse_CreatesResponseAndThread(string state)
    {
        var response = await RespondAsync(state);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await ReadAsAsync<Feed.RespondResponse>(response))!;
        body.State.ShouldBe(state);
        body.ThreadId.ShouldNotBeNull();

        await WithDbAsync(async db =>
        {
            (await db.MerchantResponses.SingleAsync()).State.ToString().ShouldBe(state);
            var thread = await db.ChatThreads.SingleAsync();
            thread.Id.ShouldBe(body.ThreadId!.Value);
            thread.PostId.ShouldBe(_postId);
            thread.MerchantId.ShouldBe(_merchantId);
            thread.IsLocked.ShouldBeFalse();
        });
    }

    [Test]
    public async Task CantHelp_CreatesResponseWithoutThread()
    {
        var body = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("CantHelp")))!;

        body.State.ShouldBe("CantHelp");
        body.ThreadId.ShouldBeNull();
        await WithDbAsync(async db =>
        {
            (await db.MerchantResponses.CountAsync()).ShouldBe(1);
            (await db.ChatThreads.CountAsync()).ShouldBe(0);
        });
    }

    [Test]
    public async Task RepeatedAndChangedResponses_UpsertWithSingleThread()
    {
        var first = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("MayHaveIt")))!;
        var second = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("MayHaveIt")))!;
        var third = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("CanOrderIt")))!;

        second.ThreadId.ShouldBe(first.ThreadId);
        third.ThreadId.ShouldBe(first.ThreadId);
        third.State.ShouldBe("CanOrderIt");
        third.UpdatedAt.ShouldBeGreaterThanOrEqualTo(first.UpdatedAt);

        await WithDbAsync(async db =>
        {
            (await db.MerchantResponses.CountAsync()).ShouldBe(1);
            (await db.ChatThreads.CountAsync()).ShouldBe(1);
        });
    }

    [Test]
    public async Task SwitchingToCantHelp_KeepsThreadAndMessages()
    {
        var first = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("HaveIt")))!;
        await WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new Gdzie.Kupic.Domain.Model.Chat.ChatMessage(
                Guid.NewGuid(), first.ThreadId!.Value, _buyerId, "hello", null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });

        var back = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("CantHelp")))!;

        back.State.ShouldBe("CantHelp");
        back.ThreadId.ShouldBe(first.ThreadId);
        await WithDbAsync(async db =>
        {
            (await db.ChatThreads.CountAsync()).ShouldBe(1);
            (await db.ChatMessages.CountAsync()).ShouldBe(1);
        });

        var again = (await ReadAsAsync<Feed.RespondResponse>(await RespondAsync("HaveIt")))!;
        again.ThreadId.ShouldBe(first.ThreadId);
        await WithDbAsync(async db => (await db.ChatThreads.CountAsync()).ShouldBe(1));
    }

    [Test]
    public async Task UnknownPost_And_NotNotifiedMerchant_GetNotFound()
    {
        (await RespondAsync("HaveIt", Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await WithDbAsync(async db =>
        {
            db.PostNotifications.RemoveRange(db.PostNotifications);
            await db.SaveChangesAsync();
        });

        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await WithDbAsync(async db => (await db.MerchantResponses.CountAsync()).ShouldBe(0));
    }

    [Test]
    public async Task MerchantWithoutOnboarding_GetsNotFound()
    {
        await AuthenticateAsync(Role.Merchant);

        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OtherNotifiedMerchant_HasIndependentResponse()
    {
        await RespondAsync("HaveIt");

        var otherMerchantId = Guid.NewGuid();
        var otherUserId = await AuthenticateAsync(Role.Merchant);
        await WithDbAsync(async db =>
        {
            db.Merchants.Add(new Merchant(otherMerchantId, "Other", null, DateTimeOffset.UtcNow));
            db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), otherMerchantId, otherUserId, DateTimeOffset.UtcNow));
            db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), _postId, otherMerchantId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });

        (await RespondAsync("CantHelp")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await WithDbAsync(async db =>
        {
            (await db.MerchantResponses.CountAsync()).ShouldBe(2);
            (await db.ChatThreads.CountAsync()).ShouldBe(1);
        });
    }

    [TestCase(PostStatus.Closed)]
    [TestCase(PostStatus.Fulfilled)]
    [TestCase(PostStatus.Expired)]
    public async Task EndedPost_ReturnsConflictWithCode(PostStatus status)
    {
        await RespondAsync("HaveIt");
        await EndPostAsync(status);

        var response = await RespondAsync("CantHelp");

        await AssertPostNotActiveAsync(response);
        await WithDbAsync(async db => (await db.MerchantResponses.SingleAsync()).State.ShouldBe(ResponseState.HaveIt));
    }

    [Test]
    public async Task EndedPost_RejectsFirstResponseAndCreatesNothing()
    {
        await EndPostAsync(PostStatus.Closed);

        await AssertPostNotActiveAsync(await RespondAsync("HaveIt"));

        await WithDbAsync(async db =>
        {
            (await db.MerchantResponses.CountAsync()).ShouldBe(0);
            (await db.ChatThreads.CountAsync()).ShouldBe(0);
        });
    }

    [Test]
    public async Task PostPastExpiry_ReturnsConflictEvenIfStillActive()
    {
        await WithDbAsync(async db =>
        {
            db.Entry(await db.Posts.SingleAsync()).Property(p => p.ExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        await AssertPostNotActiveAsync(await RespondAsync("HaveIt"));
    }

    private Task EndPostAsync(PostStatus status) => WithDbAsync(async db =>
    {
        var post = await db.Posts.SingleAsync();
        var now = DateTimeOffset.UtcNow;
        _ = status switch
        {
            PostStatus.Closed => post.TryClose(now),
            PostStatus.Fulfilled => post.TryFulfil(now),
            _ => post.TryExpire(post.ExpiresAt),
        };
        await db.SaveChangesAsync();
    });

    private static async Task AssertPostNotActiveAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().ShouldBe("post_not_active");
    }
}