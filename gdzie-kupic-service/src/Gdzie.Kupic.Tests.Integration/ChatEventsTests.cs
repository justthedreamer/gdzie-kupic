using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Chat;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Service.API.Contract.Realtime;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class ChatEventsTests : IntegrationTestBase
{
    private Guid _buyerId;
    private Guid _merchantUser1;
    private Guid _merchantUser2;
    private Guid _merchantId;
    private Guid _postId;
    private HttpClient _buyer = null!;
    private HttpClient _merchant = null!;
    private readonly List<HubConnection> _connections = [];

    private static RecordingChatChannel Chat => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingChatChannel>();
    private static RecordingNotificationChannel Notifications =>
        IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingNotificationChannel>();

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

    private Task<HttpResponseMessage> RespondAsync(string state) =>
        _merchant.PutAsJsonAsync($"/api/merchant/feed/{_postId}/response", new Feed.RespondRequest(state));

    private async Task<Guid> ThreadIdAsync()
    {
        var id = Guid.Empty;
        await WithDbAsync(async db => id = (await db.ChatThreads.SingleAsync()).Id);
        return id;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid threadId, string? body) =>
        client.PostAsync($"/api/chat/threads/{threadId}/messages",
            new FormUrlEncodedContent(body is null ? [] : new Dictionary<string, string> { ["body"] = body }));

    private void ResetRecorders()
    {
        Chat.Reset();
        Notifications.Reset();
    }

    [Test]
    public async Task FirstPositiveResponse_CreatesThreadForBothSides_AndNotifiesBuyer()
    {
        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var threadId = await ThreadIdAsync();

        Chat.Events.Select(e => (e.Event, e.ThreadId)).Distinct().ShouldBe([("threadUpdated", threadId)]);
        Chat.Events.Select(e => e.UserId).ShouldBe([_buyerId, _merchantUser1, _merchantUser2], ignoreOrder: true);
        Notifications.Events.ShouldBe([(_buyerId, NotificationKind.MerchantResponded, (Guid?)_postId, (Guid?)threadId)]);
    }

    [Test]
    public async Task CantHelp_RaisesNothing()
    {
        (await RespondAsync("CantHelp")).StatusCode.ShouldBe(HttpStatusCode.OK);

        Chat.Events.ShouldBeEmpty();
        Notifications.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task LaterResponses_NotifyOnlyOnMoveToAPositiveState_AndDoNotRecreateTheThread()
    {
        await RespondAsync("CantHelp");
        ResetRecorders();

        await RespondAsync("MayHaveIt");
        Notifications.Events.Count.ShouldBe(1);
        Chat.Events.Select(e => e.Event).Distinct().ShouldBe(["threadUpdated"]);

        ResetRecorders();
        await RespondAsync("MayHaveIt");
        await RespondAsync("HaveIt");
        Chat.Events.ShouldBeEmpty();
        Notifications.Events.Count.ShouldBe(1);

        ResetRecorders();
        await RespondAsync("CantHelp");
        Notifications.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task RejectedResponse_RaisesNothing()
    {
        await _buyer.PostAsync($"/api/posts/{_postId}/close", null);

        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        Chat.Events.ShouldBeEmpty();
        Notifications.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task BuyerMessage_ReachesEveryMerchantAccount_NotTheSender()
    {
        await RespondAsync("HaveIt");
        var threadId = await ThreadIdAsync();
        ResetRecorders();

        (await SendAsync(_buyer, threadId, "Hi")).StatusCode.ShouldBe(HttpStatusCode.Created);

        Chat.Events.Select(e => e.UserId).ShouldBe([_merchantUser1, _merchantUser2], ignoreOrder: true);
        Chat.Events.ShouldAllBe(e => e.Event == "messageReceived" && e.ThreadId == threadId && e.MessageId != null);
        Notifications.Events.Select(e => e.UserId).ShouldBe([_merchantUser1, _merchantUser2], ignoreOrder: true);
        Notifications.Events.ShouldAllBe(e => e.Kind == NotificationKind.NewMessage && e.ThreadId == threadId);
    }

    [Test]
    public async Task MerchantMessage_ReachesTheBuyerOnly()
    {
        await RespondAsync("HaveIt");
        var threadId = await ThreadIdAsync();
        ResetRecorders();

        (await SendAsync(_merchant, threadId, "Yes")).StatusCode.ShouldBe(HttpStatusCode.Created);

        Chat.Events.Select(e => e.UserId).ShouldBe([_buyerId]);
        Notifications.Events.Select(e => (e.UserId, e.Kind)).ShouldBe([(_buyerId, NotificationKind.NewMessage)]);
    }

    [Test]
    public async Task RejectedMessage_RaisesNothing()
    {
        await RespondAsync("HaveIt");
        var threadId = await ThreadIdAsync();
        await WithDbAsync(async db =>
        {
            (await db.ChatThreads.SingleAsync()).IsLocked = true;
            await db.SaveChangesAsync();
        });
        ResetRecorders();

        (await SendAsync(_buyer, threadId, "Hi")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(_merchant, threadId, "Hi")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await WithDbAsync(async db =>
        {
            (await db.ChatThreads.SingleAsync()).IsLocked = false;
            await db.SaveChangesAsync();
        });
        (await SendAsync(_buyer, threadId, null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        Chat.Events.ShouldBeEmpty();
        Notifications.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task LockChange_RaisesThreadUpdatedForBothSides()
    {
        await RespondAsync("HaveIt");
        var threadId = await ThreadIdAsync();
        ResetRecorders();

        using (var scope = IntegrationTestSetup.Factory.Services.CreateScope())
        {
            var changed = await scope.ServiceProvider.GetRequiredService<IChatStorage>().SetLockForUserAsync(_buyerId, banned: true);
            changed.ShouldBe([threadId]);
            await scope.ServiceProvider.GetRequiredService<IChatThreadEvents>().ThreadsLockChangedAsync(changed);
        }

        Chat.Events.Select(e => (e.Event, e.ThreadId)).Distinct().ShouldBe([("threadUpdated", threadId)]);
        Chat.Events.Select(e => e.UserId).ShouldBe([_buyerId, _merchantUser1, _merchantUser2], ignoreOrder: true);
    }

    [Test]
    public async Task ConnectedClients_ReceiveEachEvent()
    {
        var buyerConnection = await ConnectAsync(_buyerId);
        var threadUpdated = Next<ThreadDto>(buyerConnection, RealtimeEvents.ThreadUpdated);
        var raised = Next<NotificationDto>(buyerConnection, RealtimeEvents.NotificationRaised);
        var merchantConnection = await ConnectAsync(_merchantUser2);
        var received = Next<MessageDto>(merchantConnection, RealtimeEvents.MessageReceived);

        await RespondAsync("HaveIt");
        var threadId = await ThreadIdAsync();
        await SendAsync(_buyer, threadId, "Hi");

        (await threadUpdated).ThreadId.ShouldBe(threadId);
        var notification = await raised;
        notification.Kind.ShouldBe("merchantResponded");
        notification.PostId.ShouldBe(_postId);
        notification.ThreadId.ShouldBe(threadId);
        var message = await received;
        message.ThreadId.ShouldBe(threadId);
        message.MessageId.ShouldNotBe(Guid.Empty);
    }

    private sealed record ThreadDto(Guid ThreadId);
    private sealed record MessageDto(Guid ThreadId, Guid MessageId);
    private sealed record NotificationDto(string Kind, Guid? PostId, Guid? ThreadId);

    private static Task<T> Next<T>(HubConnection connection, string eventName)
    {
        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<T>(eventName, payload => source.TrySetResult(payload));

        return source.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

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
