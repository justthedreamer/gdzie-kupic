using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Common;
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

public class EmailNotificationTests : IntegrationTestBase
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
    private static RecordingEmailSender Mail => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingEmailSender>();

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
            foreach (var user in await db.Users.ToListAsync()) user.EmailNotificationsEnabled = true;
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

    private Task<HttpResponseMessage> SendAsync(HttpClient client, Guid threadId, string body) =>
        client.PostAsync($"/api/chat/threads/{threadId}/messages",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["body"] = body }));

    private async Task<Guid> ThreadIdAsync()
    {
        var id = Guid.Empty;
        await WithDbAsync(async db => id = (await db.ChatThreads.SingleAsync()).Id);
        return id;
    }

    private async Task DispatchAsync(Notification notification)
    {
        await RunAsync<INotificationDispatcher>(d => d.DispatchAsync(notification));
        await Scheduler.RunPendingAsync();
    }

    [Test]
    public async Task PositiveResponse_EmailsTheBuyer_InPolish_WithLinkAndOptOutHint()
    {
        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Scheduler.RunPendingAsync();

        var mail = Mail.Sent.ShouldHaveSingleItem();
        mail.Body.ShouldContain($"https://app.example.com/requests/{_postId}");
        mail.Body.ShouldContain("https://app.example.com/settings/notifications");
        mail.Subject.ShouldContain("odpowiedzia");
        await WithDbAsync(async db => mail.To.ShouldBe((await db.Users.SingleAsync(u => u.Id == _buyerId)).Email));
    }

    [Test]
    public async Task CantHelp_SendsNoEmail()
    {
        await RespondAsync("CantHelp");
        await Scheduler.RunPendingAsync();

        Mail.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task MerchantMessages_EmailOnlyTheFirstOfAnUnreadSeries_AndAgainAfterTheBuyerReads()
    {
        await RespondAsync("HaveIt");
        await Scheduler.RunPendingAsync();
        Mail.Reset();
        var threadId = await ThreadIdAsync();

        (await SendAsync(_merchant, threadId, "first secret")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await SendAsync(_merchant, threadId, "second secret")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await Scheduler.RunPendingAsync();
        var first = Mail.Sent.ShouldHaveSingleItem();
        first.Body.ShouldContain($"https://app.example.com/chat/{threadId}");
        Mail.Sent.ShouldAllBe(m => !m.Body.Contains("secret") && !m.Subject.Contains("secret"));

        await Task.Delay(20);
        (await _buyer.PostAsync($"/api/chat/threads/{threadId}/read", null)).IsSuccessStatusCode.ShouldBeTrue();
        await Task.Delay(20);
        await SendAsync(_merchant, threadId, "third");
        await Scheduler.RunPendingAsync();

        Mail.Sent.Count.ShouldBe(2);
    }

    [Test]
    public async Task BuyerMessage_EmailsNobody()
    {
        await RespondAsync("HaveIt");
        await Scheduler.RunPendingAsync();
        Mail.Reset();

        await SendAsync(_buyer, await ThreadIdAsync(), "hello");
        await Scheduler.RunPendingAsync();

        Mail.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task NoEmail_WithoutOptIn_ForBannedBuyer_OrWhenOnline()
    {
        await WithDbAsync(async db =>
        {
            foreach (var user in await db.Users.Where(u => u.Id == _buyerId).ToListAsync()) user.EmailNotificationsEnabled = false;
            await db.SaveChangesAsync();
        });
        await DispatchAsync(new Notification(NotificationKind.MerchantResponded, _buyerId, _postId, Guid.NewGuid()));
        Mail.Sent.ShouldBeEmpty();

        await WithDbAsync(async db =>
        {
            var buyer = await db.Users.SingleAsync(u => u.Id == _buyerId);
            buyer.EmailNotificationsEnabled = true;
            db.Entry(buyer).Reference(u => u.BanDetails).CurrentValue = new BanDetails(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        await DispatchAsync(new Notification(NotificationKind.MerchantResponded, _buyerId, _postId, Guid.NewGuid()));
        Mail.Sent.ShouldBeEmpty();

        await WithDbAsync(async db =>
        {
            db.Entry(await db.Users.SingleAsync(u => u.Id == _buyerId)).Reference(u => u.BanDetails).CurrentValue = null;
            await db.SaveChangesAsync();
        });
        await ConnectAsync(_buyerId);
        await DispatchAsync(new Notification(NotificationKind.MerchantResponded, _buyerId, _postId, Guid.NewGuid()));
        Mail.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task Merchants_GetNoEmailForNewPostsOrMessages()
    {
        await DispatchAsync(new Notification(NotificationKind.NewPost, _merchantUser1, _postId));
        await DispatchAsync(new Notification(NotificationKind.NewMessage, _merchantUser1, null, Guid.NewGuid()));
        await DispatchAsync(new Notification(NotificationKind.MerchantResponded, _merchantUser1, _postId, Guid.NewGuid()));

        Mail.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task SendFailure_ThrowsFromTheJob_ButNotFromTheRequest()
    {
        Mail.Failure = new InvalidOperationException("smtp down");

        (await RespondAsync("HaveIt")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await Should.ThrowAsync<InvalidOperationException>(Scheduler.RunPendingAsync);
    }
}