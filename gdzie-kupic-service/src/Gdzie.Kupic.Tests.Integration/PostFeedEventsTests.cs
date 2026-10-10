using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Service.API.Contract.Realtime;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class PostFeedEventsTests : IntegrationTestBase
{
    private Category _category = null!;
    private Tag _tag = null!;
    private Guid _buyerId;
    private Guid _merchantId;
    private Guid _merchantUser1;
    private Guid _merchantUser2;
    private Guid _otherMerchantUser;
    private Guid _postId;
    private HttpClient _buyer = null!;
    private HttpClient _merchant = null!;
    private readonly List<HubConnection> _connections = [];

    private static RecordingPostFeedChannel Channel =>
        IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingPostFeedChannel>();

    private IReadOnlyList<(string Event, Guid UserId, Guid PostId)> Events => Channel.Events;

    [SetUp]
    public async Task Seed()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);

        _buyerId = await AuthenticateAsync(Role.Buyer);
        _buyer = ClientWithCurrentToken();
        _merchantUser1 = await AuthenticateAsync(Role.Merchant);
        _merchant = ClientWithCurrentToken();
        _merchantUser2 = await AuthenticateAsync(Role.Merchant);
        _otherMerchantUser = await AuthenticateAsync(Role.Merchant);

        _merchantId = Guid.NewGuid();
        var otherMerchantId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Categories.Add(_category);
            db.Tags.Add(_tag);
            db.Merchants.AddRange(
                new Merchant(_merchantId, "Shop", null, DateTimeOffset.UtcNow),
                new Merchant(otherMerchantId, "Other", null, DateTimeOffset.UtcNow));
            db.MerchantAccounts.AddRange(
                new MerchantAccount(Guid.NewGuid(), _merchantId, _merchantUser1, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _merchantId, _merchantUser2, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), otherMerchantId, _otherMerchantUser, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        _postId = await AddPostAsync(DateTimeOffset.UtcNow.AddDays(1), notify: true);
    }

    [TearDown]
    public async Task Dispose()
    {
        _buyer.Dispose();
        _merchant.Dispose();
        foreach (var connection in _connections) await connection.DisposeAsync();
        _connections.Clear();
    }

    private HttpClient ClientWithCurrentToken()
    {
        var client = IntegrationTestSetup.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        return client;
    }

    private async Task<Guid> AddPostAsync(DateTimeOffset expiresAt, bool notify)
    {
        var id = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Posts.Add(new Post(id, _buyerId, new Coordinates(50, 19), 5m, _category.Id, _tag.Id, "Need", null, null, expiresAt,
                DateTimeOffset.UtcNow));
            if (notify) db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), id, _merchantId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        return id;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task RunAsync<TJob>(Func<TJob, Task> run) where TJob : notnull
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await run(scope.ServiceProvider.GetRequiredService<TJob>());
    }

    [TestCase("close")]
    [TestCase("fulfil")]
    public async Task EndingPost_RemovesItForEveryNotifiedAccount_AndUpdatesOwner(string action)
    {
        (await _buyer.PostAsync($"/api/posts/{_postId}/{action}", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Events.Where(e => e.Event == "postRemoved").Select(e => e.UserId).ShouldBe([_merchantUser1, _merchantUser2], ignoreOrder: true);
        Events.Where(e => e.Event == "postStatusChanged").Select(e => e.UserId).ShouldBe([_buyerId]);
        Events.ShouldAllBe(e => e.PostId == _postId);
    }

    [Test]
    public async Task RejectedEnd_SendsNothing()
    {
        await _buyer.PostAsync($"/api/posts/{_postId}/close", null);
        Channel.Reset();

        (await _buyer.PostAsync($"/api/posts/{_postId}/fulfil", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        Events.ShouldBeEmpty();
    }

    [Test]
    public async Task MakeLongLived_NotifiesOwnerOnly()
    {
        await WithDbAsync(async db =>
        {
            (await db.Posts.SingleAsync()).MarkDispatched(DateTimeOffset.UtcNow);
            db.PostNotifications.RemoveRange(db.PostNotifications);
            await db.SaveChangesAsync();
        });

        (await _buyer.PostAsync($"/api/posts/{_postId}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        Events.ShouldBe([("postStatusChanged", _buyerId, _postId)]);
    }

    [Test]
    public async Task Response_NotifiesOwner_AndRejectedResponseDoesNot()
    {
        (await _merchant.PutAsJsonAsync($"/api/merchant/feed/{_postId}/response", new Feed.RespondRequest("HaveIt")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        Events.ShouldBe([("postStatusChanged", _buyerId, _postId)]);

        await _buyer.PostAsync($"/api/posts/{_postId}/close", null);
        Channel.Reset();

        (await _merchant.PutAsJsonAsync($"/api/merchant/feed/{_postId}/response", new Feed.RespondRequest("CantHelp")))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ExpiryJob_RemovesExpiredPosts_AndIsIdempotent()
    {
        var expired = await AddPostAsync(DateTimeOffset.UtcNow.AddMinutes(-1), notify: true);

        await RunAsync<ExpirePostsJob>(job => job.RunAsync());

        Events.Where(e => e.Event == "postRemoved").ShouldAllBe(e => e.PostId == expired);
        Events.Where(e => e.Event == "postRemoved").Select(e => e.UserId).ShouldBe([_merchantUser1, _merchantUser2], ignoreOrder: true);
        Events.Single(e => e.Event == "postStatusChanged").UserId.ShouldBe(_buyerId);

        Channel.Reset();
        await RunAsync<ExpirePostsJob>(job => job.RunAsync());
        Events.ShouldBeEmpty();
    }

    [Test]
    public async Task DispatchCompletion_NotifiesOwner()
    {
        await RunAsync<NotifyMerchantsJob>(job => job.RunAsync(_postId));

        Events.ShouldBe([("postStatusChanged", _buyerId, _postId)]);
    }

    [Test]
    public async Task BatchJob_RaisesPostAddedOnlyForNewNotifications_ToEveryAccount()
    {
        var fresh = await AddPostAsync(DateTimeOffset.UtcNow.AddDays(1), notify: false);

        await RunAsync<NotifyMerchantsBatchJob>(job => job.RunAsync(fresh, [_merchantId]));

        Events.Select(e => (e.Event, e.PostId)).Distinct().ShouldBe([("postAdded", fresh)]);
        Events.Select(e => e.UserId).ShouldBe([_merchantUser1, _merchantUser2], ignoreOrder: true);

        Channel.Reset();
        await RunAsync<NotifyMerchantsBatchJob>(job => job.RunAsync(fresh, [_merchantId]));
        Events.ShouldBeEmpty();
    }

    [Test]
    public async Task NewMerchantScan_RaisesPostAddedOnlyForNewNotifications()
    {
        await WithDbAsync(async db =>
        {
            db.Merchants.Single(m => m.Id == _merchantId);
            db.MerchantBranches.Add(new MerchantBranch(Guid.NewGuid(), _merchantId, "Main", new Coordinates(50, 19), null, null, null, DateTimeOffset.UtcNow));
            db.MerchantSubscriptions.Add(new MerchantSubscription(Guid.NewGuid(), _merchantId, _category.Id, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        var fresh = await AddPostAsync(DateTimeOffset.UtcNow.AddDays(1), notify: false);

        await RunAsync<NotifyNewMerchantJob>(job => job.RunAsync(_merchantId));

        Events.Select(e => (e.Event, e.PostId)).Distinct().ShouldBe([("postAdded", fresh)]);

        Channel.Reset();
        await RunAsync<NotifyNewMerchantJob>(job => job.RunAsync(_merchantId));
        Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ConnectedClients_ReceiveTheEvents_OnlyTheAddressees()
    {
        var merchantEvent = new TaskCompletionSource<PostRemovedPayloadDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var buyerEvent = new TaskCompletionSource<PostRemovedPayloadDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherEvents = new List<string>();

        var merchantConnection = await ConnectAsync(_merchantUser2);
        merchantConnection.On<PostRemovedPayloadDto>(RealtimeEvents.PostRemoved, p => merchantEvent.TrySetResult(p));
        var buyerConnection = await ConnectAsync(_buyerId);
        buyerConnection.On<PostRemovedPayloadDto>(RealtimeEvents.PostStatusChanged, p => buyerEvent.TrySetResult(p));
        var otherConnection = await ConnectAsync(_otherMerchantUser);
        otherConnection.On<PostRemovedPayloadDto>(RealtimeEvents.PostRemoved, _ => otherEvents.Add("removed"));
        otherConnection.On<PostRemovedPayloadDto>(RealtimeEvents.PostStatusChanged, _ => otherEvents.Add("status"));

        await _buyer.PostAsync($"/api/posts/{_postId}/close", null);

        (await merchantEvent.Task.WaitAsync(TimeSpan.FromSeconds(5))).PostId.ShouldBe(_postId);
        (await buyerEvent.Task.WaitAsync(TimeSpan.FromSeconds(5))).PostId.ShouldBe(_postId);
        await Task.Delay(200);
        otherEvents.ShouldBeEmpty();
    }

    private sealed record PostRemovedPayloadDto(Guid PostId);

    private async Task<HubConnection> ConnectAsync(Guid userId)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var role = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .Where(u => u.Id == userId).Select(u => u.Role).SingleAsync();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>()
            .GenerateAccessToken(userId, role, DateTime.UtcNow.AddDays(1)).Token;

        var server = IntegrationTestSetup.Factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, RealtimeEvents.HubPath), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        _connections.Add(connection);
        await connection.StartAsync();

        return connection;
    }
}
