using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Service.API.Contract.Push;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class PushControllerTests : IntegrationTestBase
{
    private const string Url = "/api/push/subscription";
    private const string Endpoint = "https://push.example.com/send/abc";

    private static Push.SubscribeRequest Subscribe(string endpoint = Endpoint, string p256dh = "key", string auth = "auth") =>
        new(endpoint, new Push.Keys(p256dh, auth));

    private static async Task<List<(Guid UserId, string Endpoint, string P256dh)>> RowsAsync()
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return (await db.PushSubscriptions.ToListAsync()).Select(s => (s.UserId, s.Endpoint, s.Keys.P256dhKey)).ToList();
    }

    private Task<HttpResponseMessage> DeleteAsync(string endpoint) =>
        Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, Url) { Content = JsonContent.Create(new Push.UnsubscribeRequest(endpoint)) });

    [Test]
    public async Task Anonymous_IsRejected_AndAdminIsForbidden()
    {
        using var anonymous = IntegrationTestSetup.Factory.CreateClient();
        (await anonymous.PutAsJsonAsync(Url, Subscribe())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/push/vapid-public-key")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(Role.Admin);
        (await Client.PutAsJsonAsync(Url, Subscribe())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await DeleteAsync(Endpoint)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.GetAsync("/api/push/vapid-public-key")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    public async Task Subscribe_StoresAndIsIdempotent(Role role)
    {
        var userId = await AuthenticateAsync(role);

        (await Client.PutAsJsonAsync(Url, Subscribe())).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.PutAsJsonAsync(Url, Subscribe(p256dh: "rotated"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RowsAsync()).ShouldBe([(userId, Endpoint, "rotated")]);
    }

    [Test]
    public async Task Subscribe_EndpointOfAnotherUser_MovesToTheCaller()
    {
        await AuthenticateAsync(Role.Buyer);
        await Client.PutAsJsonAsync(Url, Subscribe());
        var second = await AuthenticateAsync(Role.Buyer);

        (await Client.PutAsJsonAsync(Url, Subscribe())).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RowsAsync()).Select(r => r.UserId).ShouldBe([second]);
    }

    [Test]
    public async Task Unsubscribe_RemovesOnlyTheCallersSubscription_AndIsIdempotent()
    {
        await AuthenticateAsync(Role.Buyer);
        await Client.PutAsJsonAsync(Url, Subscribe());
        await AuthenticateAsync(Role.Merchant);

        (await DeleteAsync(Endpoint)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowsAsync()).Count.ShouldBe(1);

        await Client.PutAsJsonAsync(Url, Subscribe());
        (await DeleteAsync(Endpoint)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DeleteAsync(Endpoint)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowsAsync()).ShouldBeEmpty();
    }

    [TestCase("")]
    [TestCase("http://push.example.com/x")]
    public async Task InvalidEndpoint_ReturnsBadRequest(string endpoint)
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.PutAsJsonAsync(Url, Subscribe(endpoint))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DeleteAsync(endpoint)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task MissingOrOverLongKeys_ReturnBadRequest()
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.PutAsJsonAsync(Url, new { endpoint = Endpoint })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PutAsJsonAsync(Url, Subscribe(p256dh: ""))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PutAsJsonAsync(Url, Subscribe(auth: new string('a', 300)))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task VapidPublicKey_WithoutConfiguration_Returns503WithCode()
    {
        using var factory = IntegrationTestSetup.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vapid:PublicKey"] = "",
                ["Vapid:PrivateKey"] = "",
                ["Vapid:Subject"] = "",
            })));
        using var client = factory.CreateClient();
        await AuthenticateAsync(Role.Buyer);
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync("/api/push/vapid-public-key");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).ShouldContain("push_not_configured");
    }

    [Test]
    public async Task VapidPublicKey_WithConfiguration_ReturnsIt()
    {
        using var factory = IntegrationTestSetup.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vapid:PublicKey"] = "BPublicKey",
                ["Vapid:PrivateKey"] = "private",
                ["Vapid:Subject"] = "mailto:admin@example.com",
            })));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        await AuthenticateAsync(Role.Merchant);
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync("/api/push/vapid-public-key");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsAsync<Push.VapidKeyResponse>(response))!.PublicKey.ShouldBe("BPublicKey");
    }
}
