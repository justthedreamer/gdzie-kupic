using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Realtime;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Service.API.Contract.Realtime;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class WebPushDeliveryTests : IntegrationTestBase
{
    private Guid _buyerId;
    private Guid _merchantUser1;
    private Guid _merchantUser2;
    private Guid _merchantId;
    private Guid _postId;
    private HttpClient _buyer = null!;
    private HttpClient _merchant = null!;
    private readonly List<HubConnection> _connections = [];

    private static RecordingJobScheduler Scheduler => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingJobScheduler>();
    private static RecordingWebPushSender Sender => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingWebPushSender>();

    [SetUp]
    public async Task Seed()
    {
        var category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        var tag = new Tag(Guid.NewGuid(), category.Id, "Microphones", false, DateTimeOffset.UtcNow);

        _buyerId = await AuthenticateAsync(Role.Buyer);
        _buyer = ClientWithCurrentToken();
        _merchantUser1 = await AuthenticateAsync(Role.Merchant);
        _merchant = ClientWithCurrentToken();
        _merchantUser2 = await AuthenticateAsync(Role.Merchant);

        _merchantId = Guid.NewGuid();
        _postId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Categories.Add(category);
            db.Tags.Add(tag);
            db.Merchants.Add(new Merchant(_merchantId, "Shop", null, DateTimeOffset.UtcNow));
            db.MerchantAccounts.AddRange(
                new MerchantAccount(Guid.NewGuid(), _merchantId, _merchantUser1, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _merchantId, _merchantUser2, DateTimeOffset.UtcNow));
            db.Posts.Add(new Post(_postId, _buyerId, new Coordinates(50, 19), 5m, category.Id, tag.Id, "Need a mic", null,
                null, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow));
            db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), _postId, _merchantId, DateTimeOffset.UtcNow));
            foreach (var userId in new[] { _buyerId, _merchantUser1, _merchantUser2 })
                db.PushSubscriptions.Add(new PushSubscription(Guid.NewGuid(), userId, EndpointOf(userId), DateTimeOffset.UtcNow) { Keys = new WebPushKeys("p256dh", "auth") });
            await db.SaveChangesAsync();
        });
    }

    [TearDown]
    public async Task Dispose()
    {
        _buyer.Dispose();
        _merchant.Dispose();
        foreach (var connection in _connections) await connection.DisposeAsync();
        _connections.Clear();
    }

    private static string EndpointOf(Guid userId) => $"https://push.example.com/{userId}";

    private HttpClient ClientWithCurrentToken()
    {
        var client = IntegrationTestSetup.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        return client;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task RunAsync<T>(Func<T, Task> run) where T : notnull
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await run(scope.ServiceProvider.GetRequiredService<T>());
    }

    private Task<HttpResponseMessage> RespondAsync(string state) =>
        _merchant.PutAsJsonAsync($"/api/merchant/feed/{_postId}/response", new Feed.RespondRequest(state));

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement;

    private async Task ConnectAsync(Guid userId)
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
    }

    [Test]
    public async Task NewPost_PushesToEveryAccountOfTheMerchant_AndMarksTheNotification()
    {
        await RunAsync<PostFeedEvents>(e => e.PostAddedAsync(_merchantId, _postId));
        await Scheduler.RunPendingAsync();

        Sender.Sent.Select(s => s.Target.Endpoint).ShouldBe([EndpointOf(_merchantUser1), EndpointOf(_merchantUser2)], ignoreOrder: true);
        var payload = Payload(Sender.Sent[0].Payload);
        payload.GetProperty("kind").GetString().ShouldBe("newPost");
        payload.GetProperty("postId").GetGuid().ShouldBe(_postId);
        payload.GetProperty("threadId").ValueKind.ShouldBe(JsonValueKind.Null);
        payload.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        await WithDbAsync(async db =>
        {
            var row = await db.PostNotifications.SingleAsync();
            row.Channel.ShouldBe(Gdzie.Kupic.Domain.Model.Notifications.NotificationChannel.WebPush);
            row.SentAt.ShouldNotBeNull();
        });
    }

    [Test]
    public async Task NewPost_WhenNothingWasDelivered_LeavesChannelAndSentAtEmpty()
    {
        Sender.Outcome = WebPushOutcome.Rejected;

        await RunAsync<PostFeedEvents>(e => e.PostAddedAsync(_merchantId, _postId));
        await Scheduler.RunPendingAsync();

        await WithDbAsync(async db => (await db.PostNotifications.SingleAsync()).SentAt.ShouldBeNull());
    }

    [Test]
    public async Task OnlineUser_GetsNoPush_UntilTheLastConnectionCloses()
    {
        await ConnectAsync(_merchantUser1);
        await ConnectAsync(_merchantUser1);
        var presence = IntegrationTestSetup.Factory.Services.GetRequiredService<ConnectionPresenceTracker>();
        presence.IsOnline(_merchantUser1).ShouldBeTrue();

        await RunAsync<PostFeedEvents>(e => e.PostAddedAsync(_merchantId, _postId));
        await Scheduler.RunPendingAsync();
        Sender.Sent.Select(s => s.Target.Endpoint).ShouldBe([EndpointOf(_merchantUser2)]);

        await _connections[0].StopAsync();
        await WaitAsync(() => _connections[0].State == HubConnectionState.Disconnected);
        presence.IsOnline(_merchantUser1).ShouldBeTrue();

        await _connections[1].StopAsync();
        await WaitAsync(() => !presence.IsOnline(_merchantUser1));
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++) await Task.Delay(100);
        condition().ShouldBeTrue();
    }

    [Test]
    public async Task PositiveResponse_PushesMerchantRespondedToTheBuyer_WithoutMessageContent()
    {
        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Scheduler.RunPendingAsync();

        Sender.Sent.Count.ShouldBe(1);
        Sender.Sent[0].Target.Endpoint.ShouldBe(EndpointOf(_buyerId));
        var payload = Payload(Sender.Sent[0].Payload);
        payload.GetProperty("kind").GetString().ShouldBe("merchantResponded");
        payload.GetProperty("postId").GetGuid().ShouldBe(_postId);
        payload.GetProperty("threadId").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Test]
    public async Task CantHelp_PushesNothing()
    {
        (await RespondAsync("CantHelp")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Scheduler.RunPendingAsync();

        Sender.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task NewMessage_PushesToTheOtherSideOnly_WithoutTheBody()
    {
        await RespondAsync("HaveIt");
        await Scheduler.RunPendingAsync();
        Sender.Reset();
        Guid threadId = default;
        await WithDbAsync(async db => threadId = (await db.ChatThreads.SingleAsync()).Id);

        var response = await _buyer.PostAsync($"/api/chat/threads/{threadId}/messages",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["body"] = "secret text" }));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await Scheduler.RunPendingAsync();

        Sender.Sent.Select(s => s.Target.Endpoint).ShouldBe([EndpointOf(_merchantUser1), EndpointOf(_merchantUser2)], ignoreOrder: true);
        Sender.Sent.ShouldAllBe(s => !s.Payload.Contains("secret"));
        Payload(Sender.Sent[0].Payload).GetProperty("kind").GetString().ShouldBe("newMessage");
        Payload(Sender.Sent[0].Payload).GetProperty("threadId").GetGuid().ShouldBe(threadId);
    }

    [Test]
    public async Task RejectedMessage_PushesNothing()
    {
        await RespondAsync("HaveIt");
        await Scheduler.RunPendingAsync();
        Sender.Reset();
        Guid threadId = default;
        await WithDbAsync(async db => threadId = (await db.ChatThreads.SingleAsync()).Id);

        var response = await _buyer.PostAsync($"/api/chat/threads/{threadId}/messages", new FormUrlEncodedContent([]));
        response.IsSuccessStatusCode.ShouldBeFalse();
        await Scheduler.RunPendingAsync();

        Sender.Sent.ShouldBeEmpty();
    }

    [TestCase(WebPushOutcome.SubscriptionGone)]
    public async Task GoneSubscription_IsRemovedByTheCleanupJob(WebPushOutcome outcome)
    {
        Sender.Outcome = outcome;

        await RunAsync<INotificationDispatcher>(d => d.DispatchAsync(new Notification(NotificationKind.NewMessage, _buyerId, null, Guid.NewGuid())));
        await Scheduler.RunPendingAsync();

        Scheduler.CountOf<CleanPushSubscriptionsJob>().ShouldBe(1);
        await WithDbAsync(async db =>
        {
            (await db.PushSubscriptions.AnyAsync(s => s.UserId == _buyerId)).ShouldBeFalse();
            (await db.PushSubscriptions.CountAsync()).ShouldBe(2);
        });
    }

    [Test]
    public async Task TransientFailure_ThrowsSoThatHangfireRetries()
    {
        Sender.Failure = new HttpRequestException("push service unavailable");

        await RunAsync<INotificationDispatcher>(d => d.DispatchAsync(new Notification(NotificationKind.NewMessage, _buyerId, null, Guid.NewGuid())));

        await Should.ThrowAsync<HttpRequestException>(Scheduler.RunPendingAsync);
    }

    [Test]
    public async Task WithoutVapidConfiguration_NothingIsEnqueued()
    {
        using var factory = IntegrationTestSetup.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Vapid:PublicKey"] = "", ["Vapid:PrivateKey"] = "" })));
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .DispatchAsync(new Notification(NotificationKind.NewMessage, _buyerId, null, Guid.NewGuid()));

        factory.Services.GetRequiredService<RecordingJobScheduler>().CountOf<SendWebPushJob>().ShouldBe(0);
    }
}