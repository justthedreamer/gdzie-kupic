using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Notifications;

public class PushSubscriptionServiceTests
{
    private const string Endpoint = "https://push.example.com/send/abc";

    private AppDbContext _db = null!;
    private Guid _user1;
    private Guid _user2;

    [SetUp]
    public async Task SetUp()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var users = new[]
        {
            new User(Guid.NewGuid(), "a@example.com", null, Role.Buyer, DateTimeOffset.UtcNow),
            new User(Guid.NewGuid(), "b@example.com", null, Role.Merchant, DateTimeOffset.UtcNow),
        };
        _db.Users.AddRange(users);
        await _db.SaveChangesAsync();
        _user1 = users[0].Id;
        _user2 = users[1].Id;
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private PushSubscriptionService Service(VapidSettings? vapid = null) =>
        new(new NotificationStorage(_db), Options.Create(vapid ?? new VapidSettings()), TimeProvider.System);

    [Test]
    public async Task Register_Twice_KeepsOneRowAndUpdatesKeys()
    {
        var service = Service();

        (await service.RegisterAsync(_user1, Endpoint, "k1", "a1")).IsSuccess.ShouldBeTrue();
        (await service.RegisterAsync(_user1, Endpoint, "k2", "a2")).IsSuccess.ShouldBeTrue();

        var row = await _db.PushSubscriptions.SingleAsync();
        row.UserId.ShouldBe(_user1);
        row.Keys.P256dhKey.ShouldBe("k2");
        row.Keys.AuthKey.ShouldBe("a2");
    }

    [Test]
    public async Task Register_EndpointOfAnotherUser_MovesIt()
    {
        var service = Service();
        await service.RegisterAsync(_user1, Endpoint, "k", "a");

        await service.RegisterAsync(_user2, Endpoint, "k", "a");

        (await _db.PushSubscriptions.SingleAsync()).UserId.ShouldBe(_user2);
    }

    [Test]
    public async Task Unregister_RemovesOnlyOwnSubscription_AndIsIdempotent()
    {
        var service = Service();
        await service.RegisterAsync(_user1, Endpoint, "k", "a");

        (await service.UnregisterAsync(_user2, Endpoint)).IsSuccess.ShouldBeTrue();
        (await _db.PushSubscriptions.CountAsync()).ShouldBe(1);

        (await service.UnregisterAsync(_user1, Endpoint)).IsSuccess.ShouldBeTrue();
        (await service.UnregisterAsync(_user1, Endpoint)).IsSuccess.ShouldBeTrue();
        (await _db.PushSubscriptions.CountAsync()).ShouldBe(0);
    }

    [TestCase(null, "k", "a")]
    [TestCase("", "k", "a")]
    [TestCase("http://push.example.com/x", "k", "a")]
    [TestCase("not a url", "k", "a")]
    [TestCase(Endpoint, null, "a")]
    [TestCase(Endpoint, "k", "")]
    public async Task Register_InvalidInput_IsRejected(string? endpoint, string? p256dh, string? auth)
    {
        var result = await Service().RegisterAsync(_user1, endpoint, p256dh, auth);

        result.Error.ShouldBe(PushError.Validation);
        (await _db.PushSubscriptions.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Register_OverLongValues_AreRejected()
    {
        var service = Service();

        (await service.RegisterAsync(_user1, "https://p.example.com/" + new string('x', 2100), "k", "a")).Error.ShouldBe(PushError.Validation);
        (await service.RegisterAsync(_user1, Endpoint, new string('k', 300), "a")).Error.ShouldBe(PushError.Validation);
        (await service.RegisterAsync(_user1, Endpoint, "k", new string('a', 300))).Error.ShouldBe(PushError.Validation);
    }

    [Test]
    public void VapidPublicKey_IsNullUnlessFullyConfigured()
    {
        Service().GetVapidPublicKey().ShouldBeNull();
        Service(new VapidSettings { PublicKey = "pub" }).GetVapidPublicKey().ShouldBeNull();
        Service(new VapidSettings { PublicKey = "pub", PrivateKey = "priv", Subject = "mailto:a@example.com" })
            .GetVapidPublicKey().ShouldBe("pub");
    }
}
