using System.Net;
using System.Reflection;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Common;
using Gdzie.Kupic.Realtime;
using Gdzie.Kupic.Service.API.Contract.Realtime;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class RealtimeHubTests : IntegrationTestBase
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly List<HubConnection> _connections = [];

    [TearDown]
    public async Task DisposeConnections()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
        _connections.Clear();
    }

    private async Task<(Guid UserId, string Token)> CreateUserAsync(Role role, bool banned = false, DateTime? expires = null)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", null, role, DateTimeOffset.UtcNow);
        if (banned) db.Entry(user).Reference(u => u.BanDetails).CurrentValue = new BanDetails(DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>()
            .GenerateAccessToken(user.Id, role, expires ?? DateTime.UtcNow.AddDays(1)).Token;

        return (user.Id, token);
    }

    private HubConnection CreateConnection(string? token, HttpTransportType transport = HttpTransportType.LongPolling)
    {
        var server = IntegrationTestSetup.Factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, RealtimeEvents.HubPath), options =>
            {
                options.Transports = transport;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.WebSocketFactory = async (context, ct) =>
                {
                    // Like a browser, the in-process socket carries the token in the query string.
                    var uri = token is null ? context.Uri : new Uri($"{context.Uri}&access_token={token}");

                    return await server.CreateWebSocketClient().ConnectAsync(uri, ct);
                };
                if (token is not null) options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        _connections.Add(connection);

        return connection;
    }

    private static Task<T> NextAsync<T>(HubConnection connection, string eventName)
    {
        var received = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<T>(eventName, payload => received.TrySetResult(payload));

        return received.Task.WaitAsync(Wait);
    }

    private static IRealtimeSender Sender => IntegrationTestSetup.Factory.Services.GetRequiredService<IRealtimeSender>();

    [TestCase(HttpTransportType.LongPolling)]
    [TestCase(HttpTransportType.WebSockets)]
    public async Task ValidToken_Connects_ForEveryRole(HttpTransportType transport)
    {
        foreach (var role in new[] { Role.Buyer, Role.Merchant, Role.Admin })
        {
            var (_, token) = await CreateUserAsync(role);
            var connection = CreateConnection(token, transport);

            await connection.StartAsync();

            connection.State.ShouldBe(HubConnectionState.Connected);
        }
    }

    [Test]
    public async Task MissingInvalidOrExpiredToken_IsRefused()
    {
        var (_, expired) = await CreateUserAsync(Role.Buyer, expires: DateTime.UtcNow.AddMinutes(-5));

        foreach (var token in new[] { null, "not-a-jwt", expired })
        {
            var connection = CreateConnection(token);

            var failure = await Should.ThrowAsync<HttpRequestException>(() => connection.StartAsync());

            failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task BannedAccount_IsRefused()
    {
        var (_, token) = await CreateUserAsync(Role.Buyer, banned: true);
        var connection = CreateConnection(token);

        var failure = await Should.ThrowAsync<HttpRequestException>(() => connection.StartAsync());

        failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task TokenInTheQueryString_IsAcceptedForTheHubOnly()
    {
        var (_, token) = await CreateUserAsync(Role.Buyer);

        var hub = await Client.PostAsync($"{RealtimeEvents.HubPath}/negotiate?negotiateVersion=1&access_token={token}", null);
        var rest = await Client.GetAsync($"/api/posts?scope=active&access_token={token}");

        hub.StatusCode.ShouldBe(HttpStatusCode.OK);
        rest.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PushedEvent_ReachesEveryConnectionOfThatUser_AndNobodyElse()
    {
        var (userId, token) = await CreateUserAsync(Role.Buyer);
        var (_, otherToken) = await CreateUserAsync(Role.Buyer);
        var first = CreateConnection(token);
        var second = CreateConnection(token, HttpTransportType.WebSockets);
        var other = CreateConnection(otherToken);
        var postId = Guid.NewGuid();

        var onFirst = NextAsync<RealtimeEvents.PostStatusChangedPayload>(first, RealtimeEvents.PostStatusChanged);
        var onSecond = NextAsync<RealtimeEvents.PostStatusChangedPayload>(second, RealtimeEvents.PostStatusChanged);
        var received = new List<object>();
        other.On<RealtimeEvents.PostStatusChangedPayload>(RealtimeEvents.PostStatusChanged, received.Add);
        await first.StartAsync();
        await second.StartAsync();
        await other.StartAsync();

        await Sender.SendToUserAsync(userId, RealtimeEvents.PostStatusChanged, new RealtimeEvents.PostStatusChangedPayload(postId));

        (await onFirst).PostId.ShouldBe(postId);
        (await onSecond).PostId.ShouldBe(postId);
        await Task.Delay(200);
        received.ShouldBeEmpty();
    }

    [Test]
    public async Task Payload_IsCamelCaseWithIdentifiersOnly()
    {
        var (userId, token) = await CreateUserAsync(Role.Merchant);
        var connection = CreateConnection(token);
        var raw = new TaskCompletionSource<System.Text.Json.JsonElement>();
        connection.On<System.Text.Json.JsonElement>(RealtimeEvents.NotificationRaised, raw.SetResult);
        await connection.StartAsync();
        var threadId = Guid.NewGuid();

        await Sender.SendToUserAsync(userId, RealtimeEvents.NotificationRaised,
            new RealtimeEvents.NotificationRaisedPayload(RealtimeEvents.NotificationKinds.NewMessage, null, threadId));

        var json = await raw.Task.WaitAsync(Wait);
        json.GetProperty("kind").GetString().ShouldBe("newMessage");
        json.GetProperty("postId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        json.GetProperty("threadId").GetGuid().ShouldBe(threadId);
    }

    [Test]
    public async Task ClientCannotInvokeHubMethods_OrJoinGroups()
    {
        var (_, token) = await CreateUserAsync(Role.Buyer);
        var (victim, _) = await CreateUserAsync(Role.Buyer);
        var connection = CreateConnection(token);
        await connection.StartAsync();

        foreach (var method in new[] { "JoinGroup", "AddToGroupAsync", "SendToUser", "Send" })
            await Should.ThrowAsync<HubException>(() => connection.InvokeAsync(method, $"user:{victim}"));
    }

    [Test]
    public void Hub_DeclaresNoClientCallableMethods()
    {
        var declared = typeof(AppHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetBaseDefinition().DeclaringType == typeof(AppHub));

        declared.ShouldBeEmpty();
    }

    [Test]
    public async Task Handshake_FromTheFrontendOrigin_WorksWithoutCredentials()
    {
        var (_, token) = await CreateUserAsync(Role.Buyer);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{RealtimeEvents.HubPath}/negotiate?negotiateVersion=1&access_token={token}");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:3000"]);
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }
}