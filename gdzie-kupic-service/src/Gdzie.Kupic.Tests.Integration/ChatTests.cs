using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Common;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using ChatDto = Gdzie.Kupic.Service.API.Contract.Chat.Chat;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class ChatTests : IntegrationTestBase
{
    private const string Url = "/api/chat";

    private Category _category = null!;
    private Tag _tag = null!;
    private Guid _buyerId;
    private Guid _merchantUserId;
    private Guid _merchantId;
    private Guid _postId;
    private Guid _threadId;
    private string _buyerToken = null!;
    private string _merchantToken = null!;

    [SetUp]
    public async Task Seed()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);

        _buyerId = await AuthenticateAsync(Role.Buyer);
        _buyerToken = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        _merchantUserId = await AuthenticateAsync(Role.Merchant);
        _merchantToken = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        _merchantId = Guid.NewGuid();
        (_postId, _threadId) = await AddThreadAsync(_merchantId, createMerchant: true);
    }

    private async Task<(Guid PostId, Guid ThreadId)> AddThreadAsync(
        Guid merchantId, bool createMerchant = false, Guid? buyerId = null, DateTimeOffset? createdAt = null)
    {
        var postId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var created = createdAt ?? DateTimeOffset.UtcNow.AddHours(-1);

        await WithDbAsync(async db =>
        {
            if (createMerchant)
            {
                db.Categories.Add(_category);
                db.Tags.Add(_tag);
                db.Merchants.Add(new Merchant(merchantId, "Music Shop", null, DateTimeOffset.UtcNow));
                db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), merchantId, _merchantUserId, DateTimeOffset.UtcNow));
            }

            db.Posts.Add(new Post(postId, buyerId ?? _buyerId, new Coordinates(50, 19), 5m, _category.Id, _tag.Id, "Need a mic", null,
                null, DateTimeOffset.UtcNow.AddDays(3), created));
            db.ChatThreads.Add(new ChatThread(threadId, postId, merchantId, false, created));
            await db.SaveChangesAsync();
        });

        return (postId, threadId);
    }

    private async Task<Guid> AddMessageAsync(Guid threadId, Guid senderId, string body, DateTimeOffset at)
    {
        var id = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new ChatMessage(id, threadId, senderId, body, null, at));
            await db.SaveChangesAsync();
        });
        return id;
    }

    private void As(string token) =>
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private Task<HttpResponseMessage> SendAsync(string? body, Guid? threadId = null) =>
        Client.PostAsync($"{Url}/threads/{threadId ?? _threadId}/messages",
            new FormUrlEncodedContent(body is null ? [] : new Dictionary<string, string> { ["body"] = body }));

    private async Task<T> GetAsync<T>(string path)
    {
        var response = await Client.GetAsync($"{Url}{path}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsAsync<T>(response))!;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Test]
    public async Task Anonymous_And_Admin_AreRejected()
    {
        using var anonymous = IntegrationTestSetup.Factory.CreateClient();
        (await anonymous.GetAsync($"{Url}/threads")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(Role.Admin);
        (await Client.GetAsync($"{Url}/threads")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.GetAsync($"{Url}/unread-count")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task NonParticipants_GetNotFoundOnEveryThreadEndpoint()
    {
        await AddMessageAsync(_threadId, _buyerId, "hi", DateTimeOffset.UtcNow);

        foreach (var role in new[] { Role.Buyer, Role.Merchant })
        {
            await AuthenticateAsync(role);
            if (role == Role.Merchant)
            {
                var otherMerchantId = Guid.NewGuid();
                var userId = await AuthenticateAsync(Role.Merchant);
                await WithDbAsync(async db =>
                {
                    db.Merchants.Add(new Merchant(otherMerchantId, "Other", null, DateTimeOffset.UtcNow));
                    db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), otherMerchantId, userId, DateTimeOffset.UtcNow));
                    await db.SaveChangesAsync();
                });
            }

            (await Client.GetAsync($"{Url}/threads/{_threadId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await Client.GetAsync($"{Url}/threads/{_threadId}/messages")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await SendAsync("hello")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await Client.PostAsync($"{Url}/threads/{_threadId}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.ShouldBeEmpty();
            (await GetAsync<ChatDto.UnreadCount>("/unread-count")).Count.ShouldBe(0);
        }

        await WithDbAsync(async db => (await db.ChatMessages.CountAsync()).ShouldBe(1));
    }

    [Test]
    public async Task MerchantWithoutOnboarding_GetsNotFound()
    {
        await AuthenticateAsync(Role.Merchant);

        (await Client.GetAsync($"{Url}/threads")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Inbox_ShowsOwnThreadsWithCounterpartLastMessageAndUnread()
    {
        await AddMessageAsync(_threadId, _buyerId, "Do you have it?", DateTimeOffset.UtcNow.AddMinutes(-5));
        await AddMessageAsync(_threadId, _merchantUserId, "Yes, in stock", DateTimeOffset.UtcNow.AddMinutes(-4));
        await AddMessageAsync(_threadId, _merchantUserId, "Come by today", DateTimeOffset.UtcNow.AddMinutes(-3));

        As(_buyerToken);
        var buyerThread = (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Single();
        buyerThread.Id.ShouldBe(_threadId);
        buyerThread.Post.Id.ShouldBe(_postId);
        buyerThread.Post.Title.ShouldBe("Need a mic");
        buyerThread.Post.Status.ShouldBe("Active");
        buyerThread.Counterpart.Id.ShouldBe(_merchantId);
        buyerThread.Counterpart.DisplayName.ShouldBe("Music Shop");
        buyerThread.LastMessage!.Preview.ShouldBe("Come by today");
        buyerThread.LastMessage.IsMine.ShouldBeFalse();
        buyerThread.UnreadCount.ShouldBe(2);
        buyerThread.IsLocked.ShouldBeFalse();

        As(_merchantToken);
        var merchantThread = (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Single();
        merchantThread.Counterpart.Id.ShouldBe(_buyerId);
        merchantThread.Counterpart.DisplayName.ShouldNotBeNullOrWhiteSpace();
        merchantThread.LastMessage!.IsMine.ShouldBeTrue();
        merchantThread.UnreadCount.ShouldBe(1);
    }

    [Test]
    public async Task Inbox_ShowsTheMerchantTheBuyersFirstName_NeverTheEmail()
    {
        await AddMessageAsync(_threadId, _buyerId, "Do you have it?", DateTimeOffset.UtcNow.AddMinutes(-5));
        As(_merchantToken);
        (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Single().Counterpart.DisplayName.ShouldBe("Kupuj\u0105cy");

        await WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == _buyerId)).FirstName = "Anna";
            await db.SaveChangesAsync();
        });

        var thread = (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Single();
        thread.Counterpart.DisplayName.ShouldBe("Anna");

        var single = await GetAsync<ChatDto.ThreadSummary>($"/threads/{_threadId}");
        single.Counterpart.DisplayName.ShouldBe("Anna");

        // The buyer still sees the shop's name.
        As(_buyerToken);
        (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Single().Counterpart.DisplayName.ShouldBe("Music Shop");
    }

    [Test]
    public async Task Inbox_OnlyContainsTheCallersThreads_NewestActivityFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var (_, quiet) = await AddThreadAsync(_merchantId, createdAt: now.AddHours(-5));
        var (_, noise) = await AddThreadAsync(_merchantId, createdAt: now.AddHours(-4));
        var (_, foreignBuyer) = await AddThreadAsync(_merchantId, buyerId: Guid.NewGuid());
        await AddMessageAsync(_threadId, _buyerId, "old", now.AddHours(-3));
        await AddMessageAsync(noise, _buyerId, "latest", now.AddMinutes(-1));

        As(_buyerToken);
        var ids = (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Select(i => i.Id).ToList();

        ids.ShouldBe([noise, _threadId, quiet]);
        ids.ShouldNotContain(foreignBuyer);

        As(_merchantToken);
        (await GetAsync<ChatDto.ThreadPage>("/threads")).Items.Select(i => i.Id).ShouldContain(foreignBuyer);
    }

    [Test]
    public async Task Inbox_PaginatesWithCursor()
    {
        var now = DateTimeOffset.UtcNow;
        var expected = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var (_, threadId) = await AddThreadAsync(_merchantId, createdAt: now.AddHours(-10 + i));
            expected.Insert(0, threadId);
        }
        expected.Add(_threadId);
        // _threadId was created one hour ago, so it is the most recent of all.
        expected.Remove(_threadId);
        expected.Insert(0, _threadId);

        As(_buyerToken);
        var first = await GetAsync<ChatDto.ThreadPage>("/threads?limit=2");
        first.NextCursor.ShouldNotBeNull();
        var second = await GetAsync<ChatDto.ThreadPage>($"/threads?limit=2&cursor={first.NextCursor}");
        var third = await GetAsync<ChatDto.ThreadPage>($"/threads?limit=2&cursor={second.NextCursor}");

        third.NextCursor.ShouldBeNull();
        first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Id).ShouldBe(expected);
    }

    [TestCase("/threads?limit=0")]
    [TestCase("/threads?cursor=garbage")]
    public async Task Inbox_InvalidQuery_ReturnsBadRequest(string path) =>
        (await Client.GetAsync($"{Url}{path}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Test]
    public async Task Thread_ReturnsSummaryForParticipant()
    {
        As(_buyerToken);

        var thread = await GetAsync<ChatDto.ThreadSummary>($"/threads/{_threadId}");

        thread.Id.ShouldBe(_threadId);
        thread.LastMessage.ShouldBeNull();
        thread.UnreadCount.ShouldBe(0);
        (await Client.GetAsync($"{Url}/threads/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task History_LatestPageIsAscending_AndOlderPagesLoadWithBefore()
    {
        var now = DateTimeOffset.UtcNow;
        var ids = new List<Guid>();
        for (var i = 0; i < 7; i++)
            ids.Add(await AddMessageAsync(_threadId, i % 2 == 0 ? _buyerId : _merchantUserId, $"m{i}", now.AddMinutes(-20 + i)));

        As(_buyerToken);
        var latest = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages?limit=3");
        latest.Items.Select(m => m.Id).ShouldBe(ids.Skip(4).ToList());
        latest.HasMore.ShouldBeTrue();
        latest.Items.Select(m => m.IsMine).ShouldBe([true, false, true]);

        var older = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages?limit=3&before={latest.Items[0].Id}");
        older.Items.Select(m => m.Id).ShouldBe(ids.Skip(1).Take(3).ToList());
        older.HasMore.ShouldBeTrue();

        var oldest = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages?limit=3&before={older.Items[0].Id}");
        oldest.Items.Select(m => m.Id).ShouldBe([ids[0]]);
        oldest.HasMore.ShouldBeFalse();
    }

    [Test]
    public async Task History_DefaultPageIs30_AndAfterReturnsNewerMessages()
    {
        var now = DateTimeOffset.UtcNow;
        var ids = new List<Guid>();
        for (var i = 0; i < 35; i++) ids.Add(await AddMessageAsync(_threadId, _buyerId, $"m{i}", now.AddMinutes(-60 + i)));

        As(_merchantToken);
        var latest = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages");
        latest.Items.Count.ShouldBe(30);
        latest.Items[0].Id.ShouldBe(ids[5]);
        latest.HasMore.ShouldBeTrue();

        var newer = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages?after={ids[32]}");
        newer.Items.Select(m => m.Id).ShouldBe([ids[33], ids[34]]);
        newer.HasMore.ShouldBeFalse();

        var none = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages?after={ids[34]}");
        none.Items.ShouldBeEmpty();
        none.HasMore.ShouldBeFalse();
    }

    [TestCase("limit=0")]
    public async Task History_InvalidQuery_ReturnsBadRequest(string query)
    {
        As(_buyerToken);
        (await Client.GetAsync($"{Url}/threads/{_threadId}/messages?{query}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task History_BothCursors_Or_ForeignCursor_ReturnsBadRequest()
    {
        var mine = await AddMessageAsync(_threadId, _buyerId, "a", DateTimeOffset.UtcNow.AddMinutes(-2));
        var (_, other) = await AddThreadAsync(_merchantId);
        var foreign = await AddMessageAsync(other, _buyerId, "b", DateTimeOffset.UtcNow.AddMinutes(-1));

        As(_buyerToken);
        (await Client.GetAsync($"{Url}/threads/{_threadId}/messages?before={mine}&after={mine}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"{Url}/threads/{_threadId}/messages?before={foreign}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Send_PersistsMessage_AndReportsSender()
    {
        As(_merchantToken);

        var response = await SendAsync("  Hello there  ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var message = (await ReadAsAsync<ChatDto.Message>(response))!;
        message.Body.ShouldBe("Hello there");
        message.ThreadId.ShouldBe(_threadId);
        message.SenderId.ShouldBe(_merchantUserId);
        message.IsMine.ShouldBeTrue();
        message.AttachmentUrl.ShouldBeNull();

        As(_buyerToken);
        var history = await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages");
        history.Items.Single().Id.ShouldBe(message.Id);
        history.Items.Single().IsMine.ShouldBeFalse();
    }

    [Test]
    public async Task Send_EmptyOrTooLongBody_ReturnsBadRequest()
    {
        As(_buyerToken);

        (await SendAsync(null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync("")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync("   ")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(new string('x', 2001))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(new string('x', 2000))).StatusCode.ShouldBe(HttpStatusCode.Created);

        await WithDbAsync(async db => (await db.ChatMessages.CountAsync()).ShouldBe(1));
    }

    [TestCase(PostStatus.Closed)]
    [TestCase(PostStatus.Fulfilled)]
    [TestCase(PostStatus.Expired)]
    public async Task Send_StillWorksAfterThePostEnded(PostStatus status)
    {
        await WithDbAsync(async db =>
        {
            var post = await db.Posts.SingleAsync();
            _ = status switch
            {
                PostStatus.Closed => post.TryClose(DateTimeOffset.UtcNow),
                PostStatus.Fulfilled => post.TryFulfil(DateTimeOffset.UtcNow),
                _ => post.TryExpire(post.ExpiresAt),
            };
            await db.SaveChangesAsync();
        });
        As(_buyerToken);

        (await SendAsync("still here")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await GetAsync<ChatDto.ThreadSummary>($"/threads/{_threadId}")).Post.Status.ShouldBe(status.ToString());
    }

    [Test]
    public async Task Send_ToLockedThread_ReturnsForbiddenWithCode_ButReadingStillWorks()
    {
        await AddMessageAsync(_threadId, _buyerId, "before lock", DateTimeOffset.UtcNow.AddMinutes(-1));
        await WithDbAsync(async db =>
        {
            (await db.ChatThreads.SingleAsync()).IsLocked = true;
            await db.SaveChangesAsync();
        });

        foreach (var token in new[] { _buyerToken, _merchantToken })
        {
            As(token);
            var response = await SendAsync("hello?");

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("code").GetString().ShouldBe("thread_locked");

            (await GetAsync<ChatDto.ThreadSummary>($"/threads/{_threadId}")).IsLocked.ShouldBeTrue();
            (await GetAsync<ChatDto.MessagePage>($"/threads/{_threadId}/messages")).Items.Count.ShouldBe(1);
        }

        await WithDbAsync(async db => (await db.ChatMessages.CountAsync()).ShouldBe(1));
    }

    [Test]
    public async Task Read_MovesTheCallersMarkerOnly_AndUnreadCountsFollow()
    {
        var now = DateTimeOffset.UtcNow;
        await AddMessageAsync(_threadId, _buyerId, "b1", now.AddMinutes(-10));
        await AddMessageAsync(_threadId, _merchantUserId, "m1", now.AddMinutes(-9));
        await AddMessageAsync(_threadId, _merchantUserId, "m2", now.AddMinutes(-8));
        var (_, second) = await AddThreadAsync(_merchantId);
        await AddMessageAsync(second, _merchantUserId, "m3", now.AddMinutes(-7));

        As(_buyerToken);
        (await GetAsync<ChatDto.UnreadCount>("/unread-count")).Count.ShouldBe(3);

        (await Client.PostAsync($"{Url}/threads/{_threadId}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<ChatDto.UnreadCount>("/unread-count")).Count.ShouldBe(1);
        (await GetAsync<ChatDto.ThreadSummary>($"/threads/{_threadId}")).UnreadCount.ShouldBe(0);
        (await GetAsync<ChatDto.ThreadSummary>($"/threads/{second}")).UnreadCount.ShouldBe(1);

        As(_merchantToken);
        (await GetAsync<ChatDto.UnreadCount>("/unread-count")).Count.ShouldBe(1);
        (await Client.PostAsync($"{Url}/threads/{_threadId}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<ChatDto.UnreadCount>("/unread-count")).Count.ShouldBe(0);

        // A new counterpart message becomes unread again.
        As(_buyerToken);
        (await SendAsync("one more")).StatusCode.ShouldBe(HttpStatusCode.Created);
        As(_merchantToken);
        (await GetAsync<ChatDto.UnreadCount>("/unread-count")).Count.ShouldBe(1);
        (await GetAsync<ChatDto.ThreadSummary>($"/threads/{_threadId}")).UnreadCount.ShouldBe(1);
    }

    [Test]
    public async Task SetLockForUser_LocksThreadsOfBannedBuyerOrMerchant_AndRecomputesOnUnban()
    {
        var (_, otherBuyerThread) = await AddThreadAsync(_merchantId, buyerId: Guid.NewGuid());
        var otherMerchantId = Guid.NewGuid();
        var (_, otherMerchantThread) = await AddThreadAsync(otherMerchantId);

        async Task<int> LockAsync(Guid userId, bool banned)
        {
            using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<IChatStorage>().SetLockForUserAsync(userId, banned)).Count;
        }

        async Task<Dictionary<Guid, bool>> LocksAsync()
        {
            Dictionary<Guid, bool> result = [];
            await WithDbAsync(async db => result = await db.ChatThreads.ToDictionaryAsync(t => t.Id, t => t.IsLocked));
            return result;
        }

        // Banning the buyer locks only the buyer's threads.
        await WithDbAsync(async db =>
        {
            db.Entry(await db.Users.SingleAsync(u => u.Id == _buyerId)).Reference(u => u.BanDetails).CurrentValue = new BanDetails(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        (await LockAsync(_buyerId, banned: true)).ShouldBe(2);
        var locks = await LocksAsync();
        locks[_threadId].ShouldBeTrue();
        locks[otherMerchantThread].ShouldBeTrue();
        locks[otherBuyerThread].ShouldBeFalse();

        // Banning the merchant's user locks the merchant's threads too.
        await WithDbAsync(async db =>
        {
            db.Entry(await db.Users.SingleAsync(u => u.Id == _merchantUserId)).Reference(u => u.BanDetails).CurrentValue = new BanDetails(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        (await LockAsync(_merchantUserId, banned: true)).ShouldBe(1);
        (await LocksAsync())[otherBuyerThread].ShouldBeTrue();

        // Unbanning the buyer keeps threads of the still-banned merchant locked and frees the rest.
        await WithDbAsync(async db =>
        {
            db.Entry(await db.Users.SingleAsync(u => u.Id == _buyerId)).Reference(u => u.BanDetails).CurrentValue = null;
            await db.SaveChangesAsync();
        });
        (await LockAsync(_buyerId, banned: false)).ShouldBe(1);
        locks = await LocksAsync();
        locks[_threadId].ShouldBeTrue();
        locks[otherBuyerThread].ShouldBeTrue();
        locks[otherMerchantThread].ShouldBeFalse();

        // Unbanning the merchant frees everything.
        await WithDbAsync(async db =>
        {
            db.Entry(await db.Users.SingleAsync(u => u.Id == _merchantUserId)).Reference(u => u.BanDetails).CurrentValue = null;
            await db.SaveChangesAsync();
        });
        (await LockAsync(_merchantUserId, banned: false)).ShouldBe(2);
        (await LocksAsync()).Values.ShouldAllBe(l => !l);
    }

    [Test]
    public async Task SetLockForUser_UnbanKeepsLockWhileTheMerchantEntityIsBanned()
    {
        await WithDbAsync(async db =>
        {
            (await db.Merchants.SingleAsync()).BanDetails = new BanDetails(DateTimeOffset.UtcNow);
            (await db.ChatThreads.SingleAsync()).IsLocked = true;
            await db.SaveChangesAsync();
        });

        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IChatStorage>().SetLockForUserAsync(_buyerId, banned: false)).ShouldBeEmpty();

        await WithDbAsync(async db => (await db.ChatThreads.SingleAsync()).IsLocked.ShouldBeTrue());
    }
}